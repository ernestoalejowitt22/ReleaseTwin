using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;

namespace ReleaseTwin.Hosted.Api.Tests;

public class NotificationEndpointsTests
{
    private static async Task<(Guid OrgId, Guid ProjectId)> SeedAsync(CustomWebApplicationFactory factory, PlanTier tier)
    {
        var table = factory.Services.GetRequiredService<IHostedTable>();
        var org = new Organization { Id = Guid.NewGuid(), Name = "Acme", CreatedAt = DateTimeOffset.UtcNow, PlanTier = tier };
        await table.PutItemAsync(OrganizationRepository.ToItem(org));
        var project = await new ProjectRepository(table).CreateAsync(org.Id, "web");
        return (org.Id, project.Id);
    }

    private sealed record TargetView(Guid Id, string Kind, string Url, bool Enabled, string? LastOutcome, DateTimeOffset? LastAttemptAt);

    [Fact]
    public async Task TeamAdminCanAddListPatchAndDeleteTargets()
    {
        using var factory = new CustomWebApplicationFactory { UseTestClerkAuth = true };
        var (orgId, projectId) = await SeedAsync(factory, PlanTier.Team);
        var admin = factory.CreateClientForOrg(orgId, MembershipRole.Admin);
        var basePath = $"/api/projects/{projectId}/notification-targets/";

        var created = await admin.PostAsJsonAsync(basePath, new { kind = "Slack", url = "https://hooks.example.com/abc" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var target = await created.Content.ReadFromJsonAsync<TargetView>();
        Assert.True(target!.Enabled);

        var list = await admin.GetFromJsonAsync<List<TargetView>>(basePath);
        Assert.Single(list!);

        var patched = await admin.PatchAsJsonAsync($"{basePath}{target.Id}", new { enabled = false });
        Assert.Equal(HttpStatusCode.OK, patched.StatusCode);
        var afterPatch = await admin.GetFromJsonAsync<List<TargetView>>(basePath);
        Assert.False(afterPatch![0].Enabled);

        var deleted = await admin.DeleteAsync($"{basePath}{target.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(await admin.GetFromJsonAsync<List<TargetView>>(basePath));
    }

    [Fact]
    public async Task NonHttpsAndPrivateAddressUrlsAreRejected()
    {
        using var factory = new CustomWebApplicationFactory { UseTestClerkAuth = true };
        var (orgId, projectId) = await SeedAsync(factory, PlanTier.Team);
        var admin = factory.CreateClientForOrg(orgId, MembershipRole.Admin);
        var basePath = $"/api/projects/{projectId}/notification-targets/";

        var http = await admin.PostAsJsonAsync(basePath, new { kind = "Webhook", url = "http://hooks.example.com/x" });
        Assert.Equal(HttpStatusCode.BadRequest, http.StatusCode);

        var privateIp = await admin.PostAsJsonAsync(basePath, new { kind = "Webhook", url = "https://10.0.0.9/x" });
        Assert.Equal(HttpStatusCode.BadRequest, privateIp.StatusCode);
    }

    [Fact]
    public async Task MemberIsForbidden()
    {
        using var factory = new CustomWebApplicationFactory { UseTestClerkAuth = true };
        var (orgId, projectId) = await SeedAsync(factory, PlanTier.Team);
        var member = factory.CreateClientForOrg(orgId, MembershipRole.Member);

        var response = await member.PostAsJsonAsync(
            $"/api/projects/{projectId}/notification-targets/", new { kind = "Webhook", url = "https://hooks.example.com/x" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task FreeTierIsBlockedByEntitlement()
    {
        using var factory = new CustomWebApplicationFactory { UseTestClerkAuth = true };
        var (orgId, projectId) = await SeedAsync(factory, PlanTier.Free);
        var admin = factory.CreateClientForOrg(orgId, MembershipRole.Admin);

        var response = await admin.PostAsJsonAsync(
            $"/api/projects/{projectId}/notification-targets/", new { kind = "Webhook", url = "https://hooks.example.com/x" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("runNotifications", body!["entitlement"]);
    }
}
