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
    public async Task MeOrganizationsListsTheCallersMembershipsWithRoleAndActiveFlag()
    {
        using var factory = new CustomWebApplicationFactory { UseTestClerkAuth = true };
        var table = factory.Services.GetRequiredService<IHostedTable>();
        var org = new Organization { Id = Guid.NewGuid(), Name = "Acme", CreatedAt = DateTimeOffset.UtcNow };
        await table.PutItemAsync(OrganizationRepository.ToItem(org));

        // Seed a real user + admin membership so CurrentUserAsync resolves.
        var userId = Guid.NewGuid();
        var users = new UserRepository(table);
        await users.CreateAsync(new AppUser { Id = userId, ClerkUserId = "clerk-me", DisplayName = "Me", CreatedAt = DateTimeOffset.UtcNow, OrganizationId = org.Id });
        await new MembershipRepository(table).PutAsync(new()
        {
            OrganizationId = org.Id, UserId = userId, Role = MembershipRole.Admin, CreatedAt = DateTimeOffset.UtcNow,
        });

        var client = factory.CreateClientForOrg(org.Id, MembershipRole.Admin);
        client.DefaultRequestHeaders.Add(TestClerkAuthHandler.SubHeader, "clerk-me");

        var list = await client.GetFromJsonAsync<List<Dictionary<string, object>>>("/api/me/organizations");
        var only = Assert.Single(list!);
        Assert.Equal("Acme", only["name"].ToString());
        Assert.Equal("Admin", only["role"].ToString());
        Assert.True(((System.Text.Json.JsonElement)only["active"]).GetBoolean());
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
