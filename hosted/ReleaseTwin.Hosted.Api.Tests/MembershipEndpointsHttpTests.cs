using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;

namespace ReleaseTwin.Hosted.Api.Tests;

public class MembershipEndpointsHttpTests
{
    private static async Task<Guid> SeedOrgAsync(CustomWebApplicationFactory factory)
    {
        var table = factory.Services.GetRequiredService<IHostedTable>();
        var org = new Organization { Id = Guid.NewGuid(), Name = "Acme", CreatedAt = DateTimeOffset.UtcNow };
        await table.PutItemAsync(OrganizationRepository.ToItem(org));
        return org.Id;
    }

    [Fact]
    public async Task AdminCanInviteAndListInvitations()
    {
        using var factory = new CustomWebApplicationFactory { UseTestClerkAuth = true };
        var orgId = await SeedOrgAsync(factory);
        var admin = factory.CreateClientForOrg(orgId, MembershipRole.Admin);

        var created = await admin.PostAsJsonAsync($"/api/organizations/{orgId}/invitations",
            new { email = "teammate@example.com", role = "Member" });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<InvView>();
        Assert.Equal("teammate@example.com", body!.Email);
        Assert.EndsWith($"/invitations/{body.Token}", body.AcceptUrl);

        var list = await admin.GetFromJsonAsync<List<InvView>>($"/api/organizations/{orgId}/invitations");
        Assert.Single(list!);
    }

    [Fact]
    public async Task MemberCannotInvite()
    {
        using var factory = new CustomWebApplicationFactory { UseTestClerkAuth = true };
        var orgId = await SeedOrgAsync(factory);
        var member = factory.CreateClientForOrg(orgId, MembershipRole.Member);

        var response = await member.PostAsJsonAsync($"/api/organizations/{orgId}/invitations",
            new { email = "x@example.com", role = "Member" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task InvitingForANonActiveOrganizationIsForbidden()
    {
        using var factory = new CustomWebApplicationFactory { UseTestClerkAuth = true };
        var orgId = await SeedOrgAsync(factory);
        var otherOrg = Guid.NewGuid();
        var admin = factory.CreateClientForOrg(orgId, MembershipRole.Admin);

        var response = await admin.PostAsJsonAsync($"/api/organizations/{otherOrg}/invitations",
            new { email = "x@example.com", role = "Member" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private sealed record InvView(string Token, string Email, string Role, string State, DateTimeOffset ExpiresAt, string AcceptUrl);
}
