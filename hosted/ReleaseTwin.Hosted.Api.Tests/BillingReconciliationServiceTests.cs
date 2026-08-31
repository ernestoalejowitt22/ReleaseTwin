using Microsoft.Extensions.Logging.Abstractions;
using ReleaseTwin.Hosted.Api.Billing;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

public class BillingReconciliationServiceTests
{
    private sealed record Harness(
        BillingReconciliationService Service,
        FakePolarClient Polar,
        InMemoryHostedTable Table,
        OrganizationRepository Orgs,
        ProjectRepository Projects,
        InMemoryOperatorAlertPublisher Alerts);

    private static Harness New(bool dryRun)
    {
        var table = new InMemoryHostedTable();
        var orgs = new OrganizationRepository(table);
        var projects = new ProjectRepository(table);
        var polar = new FakePolarClient();
        var writability = new ProjectWritabilityService(orgs, projects, TestEntitlements.Service);
        var options = new PolarOptions { ReconciliationDryRun = dryRun };
        var usageCounters = new UsageCounterRepository(table);
        var caseReports = new CaseReportRepository(table);
        var flagProofReports = new FlagProofReportRepository(table);
        var metrics = new BillingMetricsCollector(
            orgs, projects, usageCounters, caseReports, flagProofReports, polar, TestEntitlements.Service,
            options, new BillingMetricsOptions(), NullLogger<BillingMetricsCollector>.Instance);
        var alerts = new InMemoryOperatorAlertPublisher();
        var service = new BillingReconciliationService(orgs, projects, polar, writability, options, metrics, alerts, NullLogger<BillingReconciliationService>.Instance);
        return new Harness(service, polar, table, orgs, projects, alerts);
    }

    private static async Task<Guid> AddPaidOrgAsync(Harness h, string subscriptionId, int projectCount)
    {
        var id = Guid.NewGuid();
        await h.Table.PutItemAsync(OrganizationRepository.ToItem(new Organization
        {
            Id = id, Name = "Acme", CreatedAt = DateTimeOffset.UtcNow, PlanTier = PlanTier.Team,
        }));
        await h.Orgs.SetBillingAsync(id, BillingStatus.Active, DateTimeOffset.UtcNow, BillingCadence.Monthly, "cus_1", subscriptionId);
        for (var i = 0; i < projectCount; i++)
        {
            await h.Projects.CreateAsync(id, $"p{i}");
        }
        return id;
    }

    [Fact]
    public async Task DriftIsCorrectedTowardActualProjectCount()
    {
        var h = New(dryRun: false);
        await AddPaidOrgAsync(h, "sub_1", projectCount: 3);
        h.Polar.Subscriptions["sub_1"] = new SubscriptionInfo(1, "active");

        var result = await h.Service.RunAsync();

        Assert.Equal(1, result.CorrectionsMade);
        Assert.Equal(3, h.Polar.QuantityUpdates.Single().Quantity);
    }

    [Fact]
    public async Task MatchingQuantityIsNotCorrected()
    {
        var h = New(dryRun: false);
        await AddPaidOrgAsync(h, "sub_1", projectCount: 2);
        h.Polar.Subscriptions["sub_1"] = new SubscriptionInfo(2, "active");

        var result = await h.Service.RunAsync();

        Assert.Equal(0, result.CorrectionsMade);
        Assert.Empty(h.Polar.QuantityUpdates);
    }

    [Fact]
    public async Task UnlinkedOrgsAreSkipped()
    {
        var h = New(dryRun: false);
        var id = Guid.NewGuid();
        await h.Table.PutItemAsync(OrganizationRepository.ToItem(new Organization
        {
            Id = id, Name = "Ops", CreatedAt = DateTimeOffset.UtcNow, PlanTier = PlanTier.Enterprise,
        }));
        await h.Projects.CreateAsync(id, "a");

        var result = await h.Service.RunAsync();

        Assert.Equal(0, result.OrgsChecked);
        Assert.Equal(1, result.Skipped);
        Assert.Empty(h.Polar.QuantityUpdates);
    }

    [Fact]
    public async Task DryRunMakesNoCalls()
    {
        var h = New(dryRun: true);
        await AddPaidOrgAsync(h, "sub_1", projectCount: 1);
        h.Polar.Subscriptions["sub_1"] = new SubscriptionInfo(5, "active");

        var result = await h.Service.RunAsync();

        Assert.Equal(1, result.CorrectionsMade);
        Assert.Empty(h.Polar.QuantityUpdates);
    }
}
