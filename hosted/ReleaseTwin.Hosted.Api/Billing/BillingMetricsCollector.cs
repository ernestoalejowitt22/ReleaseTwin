using Microsoft.Extensions.Logging;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Plans;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Billing;

/// <summary>
/// billing-metrics-digest: evaluates the billing-integrity and abuse checks across every organization
/// and returns a <see cref="BillingMetricsSnapshot"/>. Read-only — it never mutates an org, a project,
/// a subscription, or a counter. Runs once a day from inside the reconciliation Lambda
/// (<see cref="BillingReconciliationService"/>), before that job applies any correction, so the drift
/// it reports is the pre-correction state.
///
/// This is the one billing path that deliberately crosses organization boundaries, same as
/// <see cref="StalenessDigestService"/>: a full <c>ListAllAsync</c> scan, then per-org partition
/// queries. Not a per-request path.
/// </summary>
public sealed class BillingMetricsCollector
{
    private readonly IOrganizationRepository _organizations;
    private readonly IProjectRepository _projects;
    private readonly IUsageCounterRepository _usageCounters;
    private readonly ICaseReportRepository _caseReports;
    private readonly IFlagProofReportRepository _flagProofReports;
    private readonly IPolarClient _polar;
    private readonly IEntitlementService _entitlements;
    private readonly PolarOptions _polarOptions;
    private readonly BillingMetricsOptions _options;
    private readonly ILogger<BillingMetricsCollector> _logger;

    public BillingMetricsCollector(
        IOrganizationRepository organizations,
        IProjectRepository projects,
        IUsageCounterRepository usageCounters,
        ICaseReportRepository caseReports,
        IFlagProofReportRepository flagProofReports,
        IPolarClient polar,
        IEntitlementService entitlements,
        PolarOptions polarOptions,
        BillingMetricsOptions options,
        ILogger<BillingMetricsCollector> logger)
    {
        _organizations = organizations;
        _projects = projects;
        _usageCounters = usageCounters;
        _caseReports = caseReports;
        _flagProofReports = flagProofReports;
        _polar = polar;
        _entitlements = entitlements;
        _polarOptions = polarOptions;
        _options = options;
        _logger = logger;
    }

    public async Task<BillingMetricsSnapshot> CollectAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var snapshot = new BillingMetricsSnapshot();

        var periodStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var periodEnd = periodStart.AddMonths(1);
        var period = DateOnly.FromDateTime(now.UtcDateTime);
        var lookbackStart = periodStart.AddDays(-_options.AnomalyLookbackDays);
        var elapsedDays = Math.Max((now - periodStart).TotalDays, 1.0);

        var organizations = await _organizations.ListAllAsync(cancellationToken);
        foreach (var org in organizations)
        {
            var projects = await _projects.ListByOrganizationAsync(org.Id, cancellationToken);

            await CollectQuantityDriftAsync(snapshot, org, projects.Count, cancellationToken);
            CollectGrace(snapshot, org, now);
            CollectReadOnlyEnforcement(snapshot, org, projects);

            var (currentRows, trailingRows) = await CountRowsAsync(projects, periodStart, periodEnd, lookbackStart, cancellationToken);

            await CollectCounterIntegrityAsync(snapshot, org, period, currentRows, cancellationToken);
            CollectVolumeAnomaly(snapshot, org, currentRows, trailingRows, elapsedDays);
            CollectFreeTierVolume(snapshot, org, currentRows);
        }

        return snapshot;
    }

    private async Task CollectQuantityDriftAsync(BillingMetricsSnapshot snapshot, Organization org, int projectCount, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(org.PolarSubscriptionId))
        {
            return;
        }

        var actual = Math.Max(projectCount, 1);
        SubscriptionInfo subscription;
        try
        {
            subscription = await _polar.GetSubscriptionAsync(org.PolarSubscriptionId!, cancellationToken);
        }
        catch (PolarException ex)
        {
            _logger.LogWarning(ex, "billing_metrics_drift_read_failed org={OrgId} subscription={SubscriptionId}", org.Id, org.PolarSubscriptionId);
            return;
        }

        if (subscription.Quantity == actual)
        {
            return;
        }

        snapshot.QuantityDrift.Add(new QuantityDriftRow(
            org.Id,
            org.Name,
            subscription.Quantity,
            actual,
            _polarOptions.ReconciliationDryRun ? DriftDisposition.Simulated : DriftDisposition.Applied,
            OverBilled: subscription.Quantity > actual));
    }

    private static void CollectGrace(BillingMetricsSnapshot snapshot, Organization org, DateTimeOffset now)
    {
        if (org.BillingStatus == BillingStatus.Active)
        {
            return;
        }

        var graceEnd = org.BillingStatusSince + EntitlementService.PastDueGraceWindow;
        var lapsed = org.BillingStatus == BillingStatus.Canceled || now > graceEnd;
        var elapsed = (int)Math.Floor((now - org.BillingStatusSince).TotalDays);
        var remaining = lapsed ? 0 : (int)Math.Ceiling((graceEnd - now).TotalDays);

        snapshot.Grace.Add(new GraceRow(
            org.Id,
            org.Name,
            org.BillingStatus,
            Math.Max(elapsed, 0),
            Math.Max(remaining, 0),
            lapsed));
    }

    private void CollectReadOnlyEnforcement(BillingMetricsSnapshot snapshot, Organization org, IReadOnlyList<Project> projects)
    {
        if (_entitlements.For(org).MaxProjects is not int max || projects.Count <= max)
        {
            return;
        }

        var writable = ProjectWritabilityService.WritableProjectIds(projects, max);
        snapshot.ReadOnlyEnforcement.Add(new ReadOnlyRow(org.Id, org.Name, writable.Count, projects.Count - writable.Count));
    }

    private async Task CollectCounterIntegrityAsync(BillingMetricsSnapshot snapshot, Organization org, DateOnly period, long currentRows, CancellationToken cancellationToken)
    {
        var counter = await _usageCounters.GetAsync(org.Id, period, cancellationToken);
        var counterValue = counter.CaseReportCount + counter.FlagProofReportCount;
        if (counterValue == currentRows)
        {
            return;
        }

        snapshot.CounterIntegrity.Add(new CounterIntegrityRow(org.Id, org.Name, counterValue, currentRows, counterValue - currentRows));
    }

    private void CollectVolumeAnomaly(BillingMetricsSnapshot snapshot, Organization org, long currentRows, long trailingRows, double elapsedDays)
    {
        var trailingRate = trailingRows / (double)_options.AnomalyLookbackDays;
        var currentRate = currentRows / elapsedDays;

        if (trailingRate > 0 && currentRate > _options.SpikeMultiplier * trailingRate)
        {
            snapshot.VolumeAnomalies.Add(new VolumeAnomalyRow(org.Id, org.Name, VolumeAnomalyKind.Spike, currentRate, trailingRate));
            return;
        }

        // "Gone quiet": a project with a real, sustained cadence (>= 1/day trailing) that has uploaded
        // nothing this period — but not on the first days of the month, when zero is just "early".
        if (trailingRate >= 1.0 && currentRows == 0 && elapsedDays >= 3)
        {
            snapshot.VolumeAnomalies.Add(new VolumeAnomalyRow(org.Id, org.Name, VolumeAnomalyKind.GoneQuiet, currentRate, trailingRate));
        }
    }

    private void CollectFreeTierVolume(BillingMetricsSnapshot snapshot, Organization org, long currentRows)
    {
        if (org.PlanTier == PlanTier.Free && currentRows > _options.FreeTierVolumeThreshold)
        {
            snapshot.FreeTierVolume.Add(new FreeTierVolumeRow(org.Id, org.Name, currentRows));
        }
    }

    /// <summary>
    /// Independent count of stored case + flag-proof report rows for the org, split into the current
    /// period and the trailing window. One native partition range query per report type per project —
    /// bounded by the org's project count (design.md D5: sampleable if that ever gets heavy).
    /// </summary>
    private async Task<(long CurrentRows, long TrailingRows)> CountRowsAsync(
        IReadOnlyList<Project> projects,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        DateTimeOffset lookbackStart,
        CancellationToken cancellationToken)
    {
        long currentRows = 0;
        long trailingRows = 0;

        foreach (var project in projects)
        {
            var cases = await _caseReports.ListByProjectInRangeAsync(project.Id, lookbackStart, periodEnd, cancellationToken);
            var flags = await _flagProofReports.ListByProjectInRangeAsync(project.Id, lookbackStart, periodEnd, cancellationToken);

            foreach (var uploadedAt in cases.Select(c => c.UploadedAt).Concat(flags.Select(f => f.UploadedAt)))
            {
                if (uploadedAt >= periodStart && uploadedAt < periodEnd)
                {
                    currentRows++;
                }
                else if (uploadedAt >= lookbackStart && uploadedAt < periodStart)
                {
                    trailingRows++;
                }
            }
        }

        return (currentRows, trailingRows);
    }
}
