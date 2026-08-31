using Microsoft.Extensions.Logging;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Billing;

/// <summary>
/// billing (design.md D6): the nightly backstop for a missed webhook, a delete-time quantity failure,
/// or a race. For every org with a <c>PolarSubscriptionId</c> it compares the Merchant-of-Record
/// quantity to the actual project count, corrects Polar to the actual count, and logs the correction.
/// It also re-evaluates read-only project state (which is derived on read from the same
/// <see cref="ProjectWritabilityService"/> resolver, so "re-evaluate" here just means logging the
/// current split for operator visibility). Ships in dry-run (<see cref="PolarOptions.ReconciliationDryRun"/>).
/// Runs as a scheduled Lambda task, same host pattern as <see cref="EvidencePurgeService"/>.
/// </summary>
public sealed class BillingReconciliationService
{
    private readonly IOrganizationRepository _organizations;
    private readonly IProjectRepository _projects;
    private readonly IPolarClient _polar;
    private readonly ProjectWritabilityService _writability;
    private readonly PolarOptions _options;
    private readonly BillingMetricsCollector _metrics;
    private readonly IOperatorAlertPublisher _alerts;
    private readonly ILogger<BillingReconciliationService> _logger;

    public BillingReconciliationService(
        IOrganizationRepository organizations,
        IProjectRepository projects,
        IPolarClient polar,
        ProjectWritabilityService writability,
        PolarOptions options,
        BillingMetricsCollector metrics,
        IOperatorAlertPublisher alerts,
        ILogger<BillingReconciliationService> logger)
    {
        _organizations = organizations;
        _projects = projects;
        _polar = polar;
        _writability = writability;
        _options = options;
        _metrics = metrics;
        _alerts = alerts;
        _logger = logger;
    }

    public sealed record Result(int OrgsChecked, int CorrectionsMade, int Skipped);

    public async Task<Result> RunAsync(CancellationToken cancellationToken = default)
    {
        // billing-metrics-digest: capture the operator-visible billing-integrity state BEFORE the
        // reconciliation loop below mutates any Polar subscription, so reported drift is pre-correction.
        // A failure here must never break reconciliation — degrade to an empty snapshot.
        BillingMetricsSnapshot snapshot;
        try
        {
            snapshot = await _metrics.CollectAsync(DateTimeOffset.UtcNow, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "billing_metrics_collect_failed");
            snapshot = new BillingMetricsSnapshot();
        }

        var dryRun = _options.ReconciliationDryRun;
        var orgs = await _organizations.ListAllAsync(cancellationToken);

        var checkedCount = 0;
        var corrections = 0;
        var skipped = 0;

        foreach (var org in orgs)
        {
            if (string.IsNullOrEmpty(org.PolarSubscriptionId))
            {
                skipped++;
                continue;
            }

            checkedCount++;

            var projects = await _projects.ListByOrganizationAsync(org.Id, cancellationToken);
            var actual = Math.Max(projects.Count, 1);

            SubscriptionInfo subscription;
            try
            {
                subscription = await _polar.GetSubscriptionAsync(org.PolarSubscriptionId!, cancellationToken);
            }
            catch (PolarException ex)
            {
                _logger.LogWarning(ex, "billing_reconciliation_read_failed org={OrgId} subscription={SubscriptionId}", org.Id, org.PolarSubscriptionId);
                continue;
            }

            var writable = ProjectWritabilityService.WritableProjectIds(projects, _writability.EffectiveMaxProjects(org));
            var readOnly = projects.Count - writable.Count;
            if (readOnly > 0)
            {
                _logger.LogInformation("billing_reconciliation_readonly org={OrgId} writable={Writable} read_only={ReadOnly}", org.Id, writable.Count, readOnly);
            }

            if (subscription.Quantity == actual)
            {
                continue;
            }

            _logger.LogInformation(
                "billing_reconciliation_correction org={OrgId} subscription={SubscriptionId} polar_quantity={PolarQuantity} actual={Actual} dry_run={DryRun}",
                org.Id, org.PolarSubscriptionId, subscription.Quantity, actual, dryRun);

            corrections++;

            if (!dryRun)
            {
                try
                {
                    await _polar.SetSubscriptionQuantityAsync(org.PolarSubscriptionId!, actual, cancellationToken);
                }
                catch (PolarException ex)
                {
                    _logger.LogWarning(ex, "billing_reconciliation_write_failed org={OrgId} subscription={SubscriptionId}", org.Id, org.PolarSubscriptionId);
                }
            }
        }

        _logger.LogInformation(
            "billing_reconciliation_run orgs_checked={OrgsChecked} corrections={Corrections} skipped={Skipped} dry_run={DryRun}",
            checkedCount, corrections, skipped, dryRun);

        await PublishDigestAsync(snapshot, cancellationToken);

        return new Result(checkedCount, corrections, skipped);
    }

    /// <summary>
    /// billing-metrics-digest: always log the run's per-section counts (so "did it run" is answerable
    /// from CloudWatch regardless), and send one operator email only when at least one section has
    /// rows. A publish failure is logged, never rethrown — the reconciliation run already succeeded.
    /// </summary>
    private async Task PublishDigestAsync(BillingMetricsSnapshot snapshot, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "billing_metrics_digest_run drift={Drift} grace={Grace} read_only={ReadOnly} counter_integrity={Counter} volume_anomalies={Anomalies} free_tier_volume={FreeTier}",
            snapshot.QuantityDrift.Count,
            snapshot.Grace.Count,
            snapshot.ReadOnlyEnforcement.Count,
            snapshot.CounterIntegrity.Count,
            snapshot.VolumeAnomalies.Count,
            snapshot.FreeTierVolume.Count);

        if (snapshot.IsEmpty)
        {
            return;
        }

        var (subject, body) = BillingMetricsDigest.Format(snapshot);
        try
        {
            await _alerts.PublishAsync(subject, body, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "billing_metrics_digest_publish_failed");
        }
    }
}
