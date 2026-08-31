using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;

namespace ReleaseTwin.Hosted.Api.Tests;

public class ShareLinkEndpointsTests
{
    private static CustomWebApplicationFactory NewFactory() => new()
    {
        UseTestClerkAuth = true,
        ExtraConfiguration = new Dictionary<string, string?> { ["FeatureFlags:evidence-sharing"] = "true" },
    };

    private static async Task<(Guid OrgId, Guid ProjectId, Guid ReportId)> SeedAsync(CustomWebApplicationFactory factory, PlanTier tier)
    {
        var table = factory.Services.GetRequiredService<IHostedTable>();
        var org = new Organization { Id = Guid.NewGuid(), Name = "Acme", CreatedAt = DateTimeOffset.UtcNow, PlanTier = tier };
        await table.PutItemAsync(OrganizationRepository.ToItem(org));
        var project = await new ProjectRepository(table).CreateAsync(org.Id, "web");
        var report = new UploadedCaseReport
        {
            Id = Guid.NewGuid(), ProjectId = project.Id, CaseId = "CLM-1", OracleLocator = "t/CLM-1",
            FixtureSha256 = "abc", Passed = false, Classification = "Infrastructure",
            CleanupStatus = "AllSucceeded", DurationMs = 1, UploadedAt = DateTimeOffset.UtcNow,
        };
        await new CaseReportRepository(table).AddAsync(report);
        return (org.Id, project.Id, report.Id);
    }

    private sealed record CreatedLink(Guid Id, string Token, string Url, DateTimeOffset ExpiresAt);
    private sealed record SharedView(string CaseId, string Result, string? Classification, bool HasEvidenceDocument);

    [Fact]
    public async Task TeamAdminCreatesListsAndRevokesAndTheTokenResolvesUnauthenticated()
    {
        using var factory = NewFactory();
        var (orgId, projectId, reportId) = await SeedAsync(factory, PlanTier.Team);
        var admin = factory.CreateClientForOrg(orgId, MembershipRole.Admin);
        var basePath = $"/api/reports/{reportId}/share-links/?projectId={projectId}";

        var created = await admin.PostAsJsonAsync(basePath, new { });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var link = await created.Content.ReadFromJsonAsync<CreatedLink>();
        Assert.EndsWith($"/share/{link!.Token}", link.Url);

        var list = await admin.GetFromJsonAsync<List<Dictionary<string, object>>>(basePath);
        Assert.Single(list!);

        // The token resolves with no auth at all.
        var anon = factory.CreateClient();
        var view = await anon.GetFromJsonAsync<SharedView>($"/api/shared-runs/{link.Token}");
        Assert.Equal("CLM-1", view!.CaseId);
        Assert.Equal("failed", view.Result);
        Assert.False(view.HasEvidenceDocument);

        var revoked = await admin.DeleteAsync($"/api/reports/{reportId}/share-links/{link.Id}?projectId={projectId}");
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);

        var afterRevoke = await anon.GetAsync($"/api/shared-runs/{link.Token}");
        Assert.Equal(HttpStatusCode.NotFound, afterRevoke.StatusCode);
    }

    [Fact]
    public async Task MemberCannotCreateAShareLink()
    {
        using var factory = NewFactory();
        var (orgId, projectId, reportId) = await SeedAsync(factory, PlanTier.Team);
        var member = factory.CreateClientForOrg(orgId, MembershipRole.Member);

        var response = await member.PostAsJsonAsync($"/api/reports/{reportId}/share-links/?projectId={projectId}", new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task FreeTierIsBlockedByEntitlement()
    {
        using var factory = NewFactory();
        var (orgId, projectId, reportId) = await SeedAsync(factory, PlanTier.Free);
        var admin = factory.CreateClientForOrg(orgId, MembershipRole.Admin);

        var response = await admin.PostAsJsonAsync($"/api/reports/{reportId}/share-links/?projectId={projectId}", new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("evidenceSharing", body!["entitlement"]);
    }

    [Fact]
    public async Task DowngradeReturns403WithoutDeletingTheLink()
    {
        using var factory = NewFactory();
        var (orgId, projectId, reportId) = await SeedAsync(factory, PlanTier.Team);
        var admin = factory.CreateClientForOrg(orgId, MembershipRole.Admin);
        var created = await admin.PostAsJsonAsync($"/api/reports/{reportId}/share-links/?projectId={projectId}", new { });
        var link = await created.Content.ReadFromJsonAsync<CreatedLink>();

        var orgs = new OrganizationRepository(factory.Services.GetRequiredService<IHostedTable>());
        await orgs.SetPlanTierAsync(orgId, PlanTier.Free);

        var anon = factory.CreateClient();
        var downgraded = await anon.GetAsync($"/api/shared-runs/{link!.Token}");
        Assert.Equal(HttpStatusCode.Forbidden, downgraded.StatusCode);

        await orgs.SetPlanTierAsync(orgId, PlanTier.Team);
        var restored = await anon.GetAsync($"/api/shared-runs/{link.Token}");
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
    }
}
