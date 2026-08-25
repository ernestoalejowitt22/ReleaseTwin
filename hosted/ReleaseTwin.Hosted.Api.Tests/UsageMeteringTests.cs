using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ReleaseTwin.Hosted.Api.Contracts;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

/// <summary>
/// usage-metering spec (usage-metering capability): exercises the real HTTP ingest path end to end,
/// including the atomic counter increment, not just the repository layer in isolation.
/// </summary>
public class UsageMeteringTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public UsageMeteringTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static IngestCaseReportRequest ValidCaseReport(string caseId) => new()
    {
        CaseId = caseId,
        OracleLocator = $"tickets/{caseId}",
        FixtureSha256 = "abc123",
        Passed = true,
        CleanupStatus = "AllSucceeded",
        DurationMs = 1,
    };

    private async Task UploadCaseReportAsync(string rawToken, string caseId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", rawToken);
        var response = await client.PostAsJsonAsync("/api/ingest/case-report", ValidCaseReport(caseId));
        response.EnsureSuccessStatusCode();
    }

    // Scenario: Reports across multiple projects in the same organization are combined
    [Fact]
    public async Task ReportsAcrossMultipleProjectsInOneOrgAreSummed()
    {
        using var scope = _factory.Services.CreateScope();
        var provisioning = scope.ServiceProvider.GetRequiredService<ProvisioningService>();
        var usage = scope.ServiceProvider.GetRequiredService<IUsageCounterRepository>();

        var user = await provisioning.GetOrCreateUserAsync(Guid.NewGuid().ToString(), "tester", null);
        var projectA = await provisioning.CreateProjectAsync(user.OrganizationId, "A");
        var projectB = await provisioning.CreateProjectAsync(user.OrganizationId, "B");
        var (_, tokenA) = await provisioning.IssueTokenAsync(projectA.Id, user.OrganizationId);
        var (_, tokenB) = await provisioning.IssueTokenAsync(projectB.Id, user.OrganizationId);

        await UploadCaseReportAsync(tokenA, "CASE-A1");
        await UploadCaseReportAsync(tokenB, "CASE-B1");

        var counter = await usage.GetAsync(user.OrganizationId, Keys.CurrentUtcPeriod());
        Assert.Equal(2, counter.CaseReportCount);
    }

    // Scenario: Reports belonging to a different organization are excluded
    [Fact]
    public async Task ReportsInADifferentOrgDoNotAffectThisOrgsCounter()
    {
        using var scope = _factory.Services.CreateScope();
        var provisioning = scope.ServiceProvider.GetRequiredService<ProvisioningService>();
        var usage = scope.ServiceProvider.GetRequiredService<IUsageCounterRepository>();

        var orgAUser = await provisioning.GetOrCreateUserAsync(Guid.NewGuid().ToString(), "alice", null);
        var orgBUser = await provisioning.GetOrCreateUserAsync(Guid.NewGuid().ToString(), "bob", null);
        var projectA = await provisioning.CreateProjectAsync(orgAUser.OrganizationId, "A");
        var projectB = await provisioning.CreateProjectAsync(orgBUser.OrganizationId, "B");
        var (_, tokenA) = await provisioning.IssueTokenAsync(projectA.Id, orgAUser.OrganizationId);
        var (_, tokenB) = await provisioning.IssueTokenAsync(projectB.Id, orgBUser.OrganizationId);

        await UploadCaseReportAsync(tokenA, "CASE-A1");
        await UploadCaseReportAsync(tokenB, "CASE-B1");
        await UploadCaseReportAsync(tokenB, "CASE-B2");

        var counterA = await usage.GetAsync(orgAUser.OrganizationId, Keys.CurrentUtcPeriod());
        var counterB = await usage.GetAsync(orgBUser.OrganizationId, Keys.CurrentUtcPeriod());
        Assert.Equal(1, counterA.CaseReportCount);
        Assert.Equal(2, counterB.CaseReportCount);
    }

    // Scenario: The count reflects only the current period
    [Fact]
    public async Task APriorPeriodsIncrementDoesNotAffectTheCurrentPeriodsCounter()
    {
        using var scope = _factory.Services.CreateScope();
        var provisioning = scope.ServiceProvider.GetRequiredService<ProvisioningService>();
        var usage = scope.ServiceProvider.GetRequiredService<IUsageCounterRepository>();

        var user = await provisioning.GetOrCreateUserAsync(Guid.NewGuid().ToString(), "tester", null);
        var project = await provisioning.CreateProjectAsync(user.OrganizationId, "P");

        var priorPeriod = new DateOnly(2020, 1, 1);
        await usage.IncrementAsync(user.OrganizationId, priorPeriod, isFlagProof: false);

        var currentCounter = await usage.GetAsync(user.OrganizationId, Keys.CurrentUtcPeriod());
        var priorCounter = await usage.GetAsync(user.OrganizationId, priorPeriod);
        Assert.Equal(0, currentCounter.CaseReportCount);
        Assert.Equal(1, priorCounter.CaseReportCount);
    }

    // Scenario: A local-only run does not appear in any count (nothing to test at the ingest layer —
    // a run without a token never reaches this API at all; covered by account-provisioning's own
    // "hosted upload is opt-in" behavior, not this repository).

    // Scenario: Usage summary reflects zero usage honestly (dashboard-level; see DashboardServiceTests)

    // Flag-proof reports also increment (distinct counter attribute)
    [Fact]
    public async Task FlagProofReportsIncrementTheirOwnCounterSeparately()
    {
        using var scope = _factory.Services.CreateScope();
        var provisioning = scope.ServiceProvider.GetRequiredService<ProvisioningService>();
        var usage = scope.ServiceProvider.GetRequiredService<IUsageCounterRepository>();

        var user = await provisioning.GetOrCreateUserAsync(Guid.NewGuid().ToString(), "tester", null);
        var project = await provisioning.CreateProjectAsync(user.OrganizationId, "P");
        var (_, raw) = await provisioning.IssueTokenAsync(project.Id, user.OrganizationId);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", raw);
        var response = await client.PostAsJsonAsync("/api/ingest/flag-proof-report", new IngestFlagProofReportRequest
        {
            CaseId = "CLM-1",
            OracleLocator = "tickets/CLM-1",
            BuildIdentity = "build-1",
            Outcome = "Passed",
        });
        response.EnsureSuccessStatusCode();

        var counter = await usage.GetAsync(user.OrganizationId, Keys.CurrentUtcPeriod());
        Assert.Equal(0, counter.CaseReportCount);
        Assert.Equal(1, counter.FlagProofReportCount);
    }
}
