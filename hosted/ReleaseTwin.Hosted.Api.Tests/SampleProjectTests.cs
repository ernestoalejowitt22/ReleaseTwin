using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ReleaseTwin.Hosted.Api.Contracts;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

public class SampleProjectServiceTests
{
    private sealed class Harness
    {
        public InMemoryHostedTable Table { get; } = new();
        public OrganizationRepository Orgs { get; }
        public ProjectRepository Projects { get; }
        public ApiTokenRepository Tokens { get; }
        public DashboardService Dashboard { get; }
        public ProvisioningService Provisioning { get; }

        public Harness()
        {
            Orgs = new OrganizationRepository(Table);
            Projects = new ProjectRepository(Table);
            Tokens = new ApiTokenRepository(Table);
            var users = new UserRepository(Table);
            Provisioning = new ProvisioningService(users, Orgs, Projects, Tokens, new TokenService(), TestEntitlements.Service);
            Dashboard = new DashboardService(Orgs, Projects, new ConnectionRepository(Table), Tokens,
                new CaseReportRepository(Table), new FlagProofReportRepository(Table), new UsageCounterRepository(Table),
                new RunEvidenceRepository(Table), TestEntitlements.Service);
        }
    }

    [Fact]
    public async Task NewOrgSeesTheSampleProjectAndGuidedPanel()
    {
        var h = new Harness();
        var user = await h.Provisioning.GetOrCreateUserAsync("clerk-1", "alice", null);

        var view = await h.Dashboard.GetDashboardViewAsync(user.OrganizationId, null);

        Assert.Contains(view.Projects, p => p.IsExample && p.Id == SampleProject.Id);
        Assert.NotNull(view.GuidedSetup);
        Assert.False(view.GuidedSetup!.HasProject);
        Assert.False(view.GuidedSetup.HasToken);
        Assert.Equal(SampleProject.Id, view.SelectedProject!.Id);
        Assert.Equal(2, view.CaseReports.Count);
        Assert.Contains(view.CaseReports, r => !r.Passed);
        Assert.Single(view.FlagProofReports);
    }

    [Fact]
    public async Task GuidedPanelReflectsRealProgress()
    {
        var h = new Harness();
        var user = await h.Provisioning.GetOrCreateUserAsync("clerk-1", "alice", null);
        var project = await h.Provisioning.CreateProjectAsync(user.OrganizationId, "real");
        await h.Provisioning.IssueTokenAsync(project.Id, user.OrganizationId);

        var view = await h.Dashboard.GetDashboardViewAsync(user.OrganizationId, null);

        Assert.True(view.GuidedSetup!.HasProject);
        Assert.True(view.GuidedSetup.HasToken);
    }

    [Fact]
    public async Task SampleDisappearsAfterTheFirstRealRun()
    {
        var h = new Harness();
        var user = await h.Provisioning.GetOrCreateUserAsync("clerk-1", "alice", null);

        await h.Orgs.MarkIngestedRealRunAsync(user.OrganizationId);
        var view = await h.Dashboard.GetDashboardViewAsync(user.OrganizationId, null);

        Assert.DoesNotContain(view.Projects, p => p.IsExample);
        Assert.Null(view.GuidedSetup);

        // ... and asking for the sample explicitly no longer resolves it.
        var direct = await h.Dashboard.GetDashboardViewAsync(user.OrganizationId, SampleProject.Id);
        Assert.Null(direct.SelectedProject);
    }

    [Fact]
    public async Task MarkIngestedIsIdempotent()
    {
        var h = new Harness();
        var user = await h.Provisioning.GetOrCreateUserAsync("clerk-1", "alice", null);

        await h.Orgs.MarkIngestedRealRunAsync(user.OrganizationId);
        await h.Orgs.MarkIngestedRealRunAsync(user.OrganizationId);

        Assert.True((await h.Orgs.GetAsync(user.OrganizationId))!.HasIngestedRealRun);
    }

    [Fact]
    public async Task SampleProjectDoesNotConsumeTheFreeProjectQuota()
    {
        var h = new Harness();
        var user = await h.Provisioning.GetOrCreateUserAsync("clerk-1", "alice", null);

        // Free tier, sample project showing — the first real project still succeeds.
        var first = await h.Provisioning.CreateProjectAsync(user.OrganizationId, "first");
        Assert.NotNull(first);

        // The second is rejected by the Free one-project limit (the sample never counted).
        await Assert.ThrowsAsync<ProjectLimitExceededException>(
            () => h.Provisioning.CreateProjectAsync(user.OrganizationId, "second"));
    }
}

public class SampleProjectHttpTests
{
    private static async Task<(HttpClient Web, Guid OrgId)> WebClientAsync(CustomWebApplicationFactory factory)
    {
        var table = factory.Services.GetRequiredService<IHostedTable>();
        var org = new Organization { Id = Guid.NewGuid(), Name = "Acme", CreatedAt = DateTimeOffset.UtcNow };
        await table.PutItemAsync(OrganizationRepository.ToItem(org));
        return (factory.CreateClientForOrg(org.Id, MembershipRole.Admin), org.Id);
    }

    [Fact]
    public async Task IssuingATokenForTheSampleProjectIsRejected()
    {
        using var factory = new CustomWebApplicationFactory { UseTestClerkAuth = true };
        var (web, _) = await WebClientAsync(factory);

        var response = await web.PostAsync($"/api/dashboard/projects/{SampleProject.Id}/tokens", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TheSampleEvidenceDrillDownServesCannedData()
    {
        using var factory = new CustomWebApplicationFactory { UseTestClerkAuth = true };
        var (web, _) = await WebClientAsync(factory);

        var response = await web.GetAsync(
            $"/api/dashboard/reports/{SampleProject.FailingReportId}/evidence?projectId={SampleProject.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, System.Text.Json.JsonElement>>();
        Assert.Equal("ORD-REFUND-7", body!["document"].GetProperty("caseId").GetString());

        // A sample report with no canned evidence 403s (project not owned) rather than leaking anything.
        var passing = await web.GetAsync(
            $"/api/dashboard/reports/{SampleProject.PassingReportId}/evidence?projectId={SampleProject.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, passing.StatusCode);
    }

    [Fact]
    public async Task FirstRealIngestClearsTheSampleFromTheDashboard()
    {
        using var factory = new CustomWebApplicationFactory { UseTestClerkAuth = true };
        using var scope = factory.Services.CreateScope();
        var provisioning = scope.ServiceProvider.GetRequiredService<ProvisioningService>();
        var user = await provisioning.GetOrCreateUserAsync(Guid.NewGuid().ToString(), "t", null);
        var project = await provisioning.CreateProjectAsync(user.OrganizationId, "P");
        var (_, raw) = await provisioning.IssueTokenAsync(project.Id, user.OrganizationId);

        var web = factory.CreateClientForOrg(user.OrganizationId, MembershipRole.Admin);
        var before = await web.GetFromJsonAsync<Dictionary<string, System.Text.Json.JsonElement>>("/api/dashboard");
        Assert.True(before!["guidedSetup"].ValueKind != System.Text.Json.JsonValueKind.Null);

        var ingest = factory.CreateClient();
        ingest.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", raw);
        await ingest.PostAsJsonAsync("/api/ingest/case-report", new IngestCaseReportRequest
        {
            CaseId = "C1", OracleLocator = "t/C1", FixtureSha256 = "x", Passed = true,
            CleanupStatus = "AllSucceeded", DurationMs = 1,
        });

        var after = await web.GetFromJsonAsync<Dictionary<string, System.Text.Json.JsonElement>>("/api/dashboard");
        Assert.Equal(System.Text.Json.JsonValueKind.Null, after!["guidedSetup"].ValueKind);
        var projects = after["projects"].EnumerateArray().ToList();
        Assert.DoesNotContain(projects, p => p.GetProperty("isExample").GetBoolean());
    }
}
