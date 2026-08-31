using Microsoft.Extensions.Logging.Abstractions;
using ReleaseTwin.Hosted.Api.Billing;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

/// <summary>
/// billing-metrics-digest: each billing-integrity / abuse check in isolation, plus the reconciliation
/// job's digest wiring. Same hand-rolled-fake pattern as <see cref="BillingReconciliationServiceTests"/>
/// and <see cref="StalenessDigestServiceTests"/>.
/// </summary>
public class BillingMetricsCollectorTests
{
    // A fixed "now" mid-month so period math (period start = the 1st, ~14 days elapsed, 28-day
    // trailing window) is deterministic regardless of when the suite runs.
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PeriodStart = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Period = DateOnly.FromDateTime(Now.UtcDateTime);

    private sealed record Harness(
        BillingMetricsCollector Collector,
        InMemoryHostedTable Table,
        OrganizationRepository Orgs,
        ProjectRepository Projects,
        CaseReportRepository CaseReports,
        FlagProofReportRepository FlagProofReports,
        UsageCounterRepository UsageCounters,
        FakePolarClient Polar);

    private static Harness New(BillingMetricsOptions? options = null, bool dryRun = true)
    {
        var table = new InMemoryHostedTable();
        var orgs = new OrganizationRepository(table);
        var projects = new ProjectRepository(table);
        var caseReports = new CaseReportRepository(table);
        var flagProofReports = new FlagProofReportRepository(table);
        var usageCounters = new UsageCounterRepository(table);
        var polar = new FakePolarClient();
        var collector = new BillingMetricsCollector(
            orgs, projects, usageCounters, caseReports, flagProofReports, polar, TestEntitlements.Service,
            new PolarOptions { ReconciliationDryRun = dryRun },
            options ?? new BillingMetricsOptions(),
            NullLogger<BillingMetricsCollector>.Instance);
        return new Harness(collector, table, orgs, projects, caseReports, flagProofReports, usageCounters, polar);
    }

    private static async Task<Guid> AddOrgAsync(Harness h, PlanTier tier = PlanTier.Free, string? subscriptionId = null,
        BillingStatus status = BillingStatus.Active, DateTimeOffset? statusSince = null)
    {
        var id = Guid.NewGuid();
        await h.Table.PutItemAsync(OrganizationRepository.ToItem(new Organization
        {
            Id = id,
            Name = $"Org-{id.ToString()[..8]}",
            CreatedAt = Now.AddMonths(-6),
            PlanTier = tier,
        }));
        if (subscriptionId is not null || status != BillingStatus.Active)
        {
            await h.Orgs.SetBillingAsync(id, status, statusSince ?? Now, BillingCadence.Monthly, "cus_1", subscriptionId);
        }

        return id;
    }

    private static async Task SeedCasesAsync(Harness h, Guid projectId, int count, DateTimeOffset at)
    {
        for (var i = 0; i < count; i++)
        {
            await h.CaseReports.AddAsync(new UploadedCaseReport
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                CaseId = $"C-{Guid.NewGuid()}",
                OracleLocator = "tickets/C",
                FixtureSha256 = "abc",
                Passed = true,
                CleanupStatus = "AllSucceeded",
                UploadedAt = at,
            });
        }
    }

    // ---- Quantity drift -----------------------------------------------------------------------

    [Fact]
    public async Task Drift_simulated_when_dry_run()
    {
        var h = New(dryRun: true);
        var org = await AddOrgAsync(h, PlanTier.Team, subscriptionId: "sub_1");
        await h.Projects.CreateAsync(org, "p1");
        await h.Projects.CreateAsync(org, "p2");
        h.Polar.Subscriptions["sub_1"] = new SubscriptionInfo(5, "active");

        var snap = await h.Collector.CollectAsync(Now);

        var row = Assert.Single(snap.QuantityDrift);
        Assert.Equal(5, row.BilledQuantity);
        Assert.Equal(2, row.ActualProjectCount);
        Assert.Equal(DriftDisposition.Simulated, row.Disposition);
        Assert.True(row.OverBilled);
    }

    [Fact]
    public async Task Drift_applied_when_not_dry_run_and_underbilled()
    {
        var h = New(dryRun: false);
        var org = await AddOrgAsync(h, PlanTier.Team, subscriptionId: "sub_1");
        await h.Projects.CreateAsync(org, "p1");
        await h.Projects.CreateAsync(org, "p2");
        await h.Projects.CreateAsync(org, "p3");
        h.Polar.Subscriptions["sub_1"] = new SubscriptionInfo(1, "active");

        var snap = await h.Collector.CollectAsync(Now);

        var row = Assert.Single(snap.QuantityDrift);
        Assert.Equal(DriftDisposition.Applied, row.Disposition);
        Assert.False(row.OverBilled);
    }

    [Fact]
    public async Task Drift_none_when_quantity_matches()
    {
        var h = New();
        var org = await AddOrgAsync(h, PlanTier.Team, subscriptionId: "sub_1");
        await h.Projects.CreateAsync(org, "p1");
        h.Polar.Subscriptions["sub_1"] = new SubscriptionInfo(1, "active");

        var snap = await h.Collector.CollectAsync(Now);

        Assert.Empty(snap.QuantityDrift);
    }

    [Fact]
    public async Task Drift_none_for_org_without_subscription()
    {
        var h = New();
        var org = await AddOrgAsync(h, PlanTier.Enterprise);
        await h.Projects.CreateAsync(org, "p1");
        await h.Projects.CreateAsync(org, "p2");

        var snap = await h.Collector.CollectAsync(Now);

        Assert.Empty(snap.QuantityDrift);
    }

    // ---- Grace ------------------------------------------------------------------------------------

    [Fact]
    public async Task Grace_within_window_reports_days()
    {
        var h = New();
        await AddOrgAsync(h, PlanTier.Team, subscriptionId: "sub_1", status: BillingStatus.PastDue, statusSince: Now.AddDays(-4));

        var snap = await h.Collector.CollectAsync(Now);

        var row = Assert.Single(snap.Grace);
        Assert.False(row.Lapsed);
        Assert.Equal(4, row.DaysElapsed);
        Assert.Equal(10, row.DaysRemaining);
    }

    [Fact]
    public async Task Grace_lapsed_when_past_window()
    {
        var h = New();
        await AddOrgAsync(h, PlanTier.Team, subscriptionId: "sub_1", status: BillingStatus.PastDue, statusSince: Now.AddDays(-20));

        var snap = await h.Collector.CollectAsync(Now);

        var row = Assert.Single(snap.Grace);
        Assert.True(row.Lapsed);
        Assert.Equal(0, row.DaysRemaining);
    }

    [Fact]
    public async Task Grace_none_for_active_org()
    {
        var h = New();
        await AddOrgAsync(h, PlanTier.Team, subscriptionId: "sub_1");

        var snap = await h.Collector.CollectAsync(Now);

        Assert.Empty(snap.Grace);
    }

    // ---- Read-only enforcement ------------------------------------------------------------------

    [Fact]
    public async Task ReadOnly_split_when_over_limit()
    {
        var h = New();
        // Free tier allows 1 project; give the org 3.
        var org = await AddOrgAsync(h, PlanTier.Free);
        await h.Projects.CreateAsync(org, "p1");
        await h.Projects.CreateAsync(org, "p2");
        await h.Projects.CreateAsync(org, "p3");

        var snap = await h.Collector.CollectAsync(Now);

        var row = Assert.Single(snap.ReadOnlyEnforcement);
        Assert.Equal(1, row.WritableCount);
        Assert.Equal(2, row.ReadOnlyCount);
    }

    [Fact]
    public async Task ReadOnly_none_when_within_limit()
    {
        var h = New();
        var org = await AddOrgAsync(h, PlanTier.Free);
        await h.Projects.CreateAsync(org, "p1");

        var snap = await h.Collector.CollectAsync(Now);

        Assert.Empty(snap.ReadOnlyEnforcement);
    }

    // ---- Usage-counter integrity ---------------------------------------------------------------

    [Fact]
    public async Task CounterIntegrity_flags_mismatch()
    {
        var h = New();
        var org = await AddOrgAsync(h, PlanTier.Team, subscriptionId: "sub_1");
        var project = await h.Projects.CreateAsync(org, "p1");
        h.Polar.Subscriptions["sub_1"] = new SubscriptionInfo(1, "active");
        await SeedCasesAsync(h, project.Id, 3, PeriodStart.AddDays(2));
        // Counter says 5, stored rows are 3.
        for (var i = 0; i < 5; i++)
        {
            await h.UsageCounters.IncrementAsync(org, Period, isFlagProof: false);
        }

        var snap = await h.Collector.CollectAsync(Now);

        var row = Assert.Single(snap.CounterIntegrity);
        Assert.Equal(5, row.CounterValue);
        Assert.Equal(3, row.StoredRowCount);
        Assert.Equal(2, row.Difference);
    }

    [Fact]
    public async Task CounterIntegrity_silent_when_matching()
    {
        var h = New();
        var org = await AddOrgAsync(h, PlanTier.Team, subscriptionId: "sub_1");
        var project = await h.Projects.CreateAsync(org, "p1");
        h.Polar.Subscriptions["sub_1"] = new SubscriptionInfo(1, "active");
        await SeedCasesAsync(h, project.Id, 4, PeriodStart.AddDays(2));
        for (var i = 0; i < 4; i++)
        {
            await h.UsageCounters.IncrementAsync(org, Period, isFlagProof: false);
        }

        var snap = await h.Collector.CollectAsync(Now);

        Assert.Empty(snap.CounterIntegrity);
    }

    // ---- Volume anomaly -----------------------------------------------------------------------

    [Fact]
    public async Task Volume_spike_flagged()
    {
        var h = New();
        var org = await AddOrgAsync(h, PlanTier.Team, subscriptionId: "sub_1");
        var project = await h.Projects.CreateAsync(org, "p1");
        h.Polar.Subscriptions["sub_1"] = new SubscriptionInfo(1, "active");
        // Trailing: 28 uploads over 28 days = 1/day. Current: 200 over ~14 days ≈ 14/day > 5×.
        await SeedCasesAsync(h, project.Id, 28, PeriodStart.AddDays(-14));
        await SeedCasesAsync(h, project.Id, 200, PeriodStart.AddDays(5));

        var snap = await h.Collector.CollectAsync(Now);

        var row = Assert.Single(snap.VolumeAnomalies);
        Assert.Equal(VolumeAnomalyKind.Spike, row.Kind);
    }

    [Fact]
    public async Task Volume_gone_quiet_flagged()
    {
        var h = New();
        var org = await AddOrgAsync(h, PlanTier.Team, subscriptionId: "sub_1");
        var project = await h.Projects.CreateAsync(org, "p1");
        h.Polar.Subscriptions["sub_1"] = new SubscriptionInfo(1, "active");
        // Trailing: 60 uploads over 28 days ≈ 2/day. Current: zero.
        await SeedCasesAsync(h, project.Id, 60, PeriodStart.AddDays(-10));

        var snap = await h.Collector.CollectAsync(Now);

        var row = Assert.Single(snap.VolumeAnomalies);
        Assert.Equal(VolumeAnomalyKind.GoneQuiet, row.Kind);
    }

    [Fact]
    public async Task Volume_normal_cadence_not_flagged()
    {
        var h = New();
        var org = await AddOrgAsync(h, PlanTier.Team, subscriptionId: "sub_1");
        var project = await h.Projects.CreateAsync(org, "p1");
        h.Polar.Subscriptions["sub_1"] = new SubscriptionInfo(1, "active");
        await SeedCasesAsync(h, project.Id, 28, PeriodStart.AddDays(-14));
        await SeedCasesAsync(h, project.Id, 14, PeriodStart.AddDays(5));

        var snap = await h.Collector.CollectAsync(Now);

        Assert.Empty(snap.VolumeAnomalies);
    }

    // ---- Free-tier volume ---------------------------------------------------------------------

    [Fact]
    public async Task FreeTier_over_threshold_listed()
    {
        var h = New(new BillingMetricsOptions { FreeTierVolumeThreshold = 10 });
        var org = await AddOrgAsync(h, PlanTier.Free);
        var project = await h.Projects.CreateAsync(org, "p1");
        await SeedCasesAsync(h, project.Id, 25, PeriodStart.AddDays(3));

        var snap = await h.Collector.CollectAsync(Now);

        var row = Assert.Single(snap.FreeTierVolume);
        Assert.Equal(25, row.PeriodVolume);
    }

    [Fact]
    public async Task FreeTier_under_threshold_not_listed()
    {
        var h = New(new BillingMetricsOptions { FreeTierVolumeThreshold = 100 });
        var org = await AddOrgAsync(h, PlanTier.Free);
        var project = await h.Projects.CreateAsync(org, "p1");
        await SeedCasesAsync(h, project.Id, 25, PeriodStart.AddDays(3));

        var snap = await h.Collector.CollectAsync(Now);

        Assert.Empty(snap.FreeTierVolume);
    }

    [Fact]
    public async Task Paid_tier_high_volume_is_not_a_free_tier_row()
    {
        var h = New(new BillingMetricsOptions { FreeTierVolumeThreshold = 10 });
        var org = await AddOrgAsync(h, PlanTier.Team, subscriptionId: "sub_1");
        var project = await h.Projects.CreateAsync(org, "p1");
        h.Polar.Subscriptions["sub_1"] = new SubscriptionInfo(1, "active");
        await SeedCasesAsync(h, project.Id, 50, PeriodStart.AddDays(3));

        var snap = await h.Collector.CollectAsync(Now);

        Assert.Empty(snap.FreeTierVolume);
    }

    // ---- Digest formatting + wiring ----------------------------------------------------------

    [Fact]
    public void Digest_omits_empty_sections_and_names_dominant_condition()
    {
        var snap = new BillingMetricsSnapshot();
        snap.QuantityDrift.Add(new QuantityDriftRow(Guid.NewGuid(), "Acme", 5, 2, DriftDisposition.Simulated, OverBilled: true));

        var (subject, body) = BillingMetricsDigest.Format(snap);

        Assert.Contains("over-billing", subject);
        Assert.Contains("Acme", body);
        Assert.DoesNotContain("Read-only enforcement", body);
    }

    [Fact]
    public async Task Reconciliation_run_publishes_digest_when_conditions_present()
    {
        var table = new InMemoryHostedTable();
        var orgs = new OrganizationRepository(table);
        var projects = new ProjectRepository(table);
        var polar = new FakePolarClient();
        var usageCounters = new UsageCounterRepository(table);
        var caseReports = new CaseReportRepository(table);
        var flagProofReports = new FlagProofReportRepository(table);
        var options = new PolarOptions { ReconciliationDryRun = true };
        var collector = new BillingMetricsCollector(orgs, projects, usageCounters, caseReports, flagProofReports, polar,
            TestEntitlements.Service, options, new BillingMetricsOptions(), NullLogger<BillingMetricsCollector>.Instance);
        var alerts = new InMemoryOperatorAlertPublisher();
        var service = new BillingReconciliationService(orgs, projects, polar,
            new ProjectWritabilityService(orgs, projects, TestEntitlements.Service), options, collector, alerts,
            NullLogger<BillingReconciliationService>.Instance);

        var id = Guid.NewGuid();
        await table.PutItemAsync(OrganizationRepository.ToItem(new Organization
        {
            Id = id, Name = "Acme", CreatedAt = DateTimeOffset.UtcNow, PlanTier = PlanTier.Team,
        }));
        await orgs.SetBillingAsync(id, BillingStatus.Active, DateTimeOffset.UtcNow, BillingCadence.Monthly, "cus_1", "sub_1");
        await projects.CreateAsync(id, "p1");
        polar.Subscriptions["sub_1"] = new SubscriptionInfo(9, "active");

        await service.RunAsync();

        var publish = Assert.Single(alerts.Published);
        Assert.Contains("Acme", publish.Message);
    }

    [Fact]
    public async Task Reconciliation_run_sends_nothing_when_clean()
    {
        var h = New();
        var org = await AddOrgAsync(h, PlanTier.Team, subscriptionId: "sub_1");
        await h.Projects.CreateAsync(org, "p1");
        h.Polar.Subscriptions["sub_1"] = new SubscriptionInfo(1, "active");
        var alerts = new InMemoryOperatorAlertPublisher();
        var service = new BillingReconciliationService(h.Orgs, h.Projects, h.Polar,
            new ProjectWritabilityService(h.Orgs, h.Projects, TestEntitlements.Service),
            new PolarOptions { ReconciliationDryRun = true }, h.Collector, alerts,
            NullLogger<BillingReconciliationService>.Instance);

        await service.RunAsync();

        Assert.Empty(alerts.Published);
    }

    [Fact]
    public async Task Reconciliation_run_survives_a_failing_alert_channel()
    {
        // spec: a publish failure (an unconfigured / unreachable channel) must not break the run.
        var h = New();
        var org = await AddOrgAsync(h, PlanTier.Team, subscriptionId: "sub_1");
        await h.Projects.CreateAsync(org, "p1");
        h.Polar.Subscriptions["sub_1"] = new SubscriptionInfo(9, "active");
        var alerts = new InMemoryOperatorAlertPublisher { ThrowOnPublish = new InvalidOperationException("no topic") };
        var service = new BillingReconciliationService(h.Orgs, h.Projects, h.Polar,
            new ProjectWritabilityService(h.Orgs, h.Projects, TestEntitlements.Service),
            new PolarOptions { ReconciliationDryRun = true }, h.Collector, alerts,
            NullLogger<BillingReconciliationService>.Instance);

        var result = await service.RunAsync();

        Assert.Equal(1, result.CorrectionsMade);
    }
}
