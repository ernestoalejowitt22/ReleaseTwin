using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

public class OrganizationAccessGuardTests
{
    [Theory]
    [InlineData(MembershipRole.Admin, OrgCapability.ManageBilling, true)]
    [InlineData(MembershipRole.Admin, OrgCapability.ManageTokens, true)]
    [InlineData(MembershipRole.Admin, OrgCapability.ManageMembers, true)]
    [InlineData(MembershipRole.Admin, OrgCapability.ManageNotifications, true)]
    [InlineData(MembershipRole.Admin, OrgCapability.ManageSharing, true)]
    [InlineData(MembershipRole.Admin, OrgCapability.ExportData, true)]
    [InlineData(MembershipRole.Admin, OrgCapability.UseProjects, true)]
    [InlineData(MembershipRole.Admin, OrgCapability.ViewEvidence, true)]
    [InlineData(MembershipRole.Member, OrgCapability.ManageBilling, false)]
    [InlineData(MembershipRole.Member, OrgCapability.ManageTokens, false)]
    [InlineData(MembershipRole.Member, OrgCapability.ManageMembers, false)]
    [InlineData(MembershipRole.Member, OrgCapability.ManageNotifications, false)]
    [InlineData(MembershipRole.Member, OrgCapability.ManageSharing, false)]
    [InlineData(MembershipRole.Member, OrgCapability.ExportData, false)]
    [InlineData(MembershipRole.Member, OrgCapability.UseProjects, true)]
    [InlineData(MembershipRole.Member, OrgCapability.ViewEvidence, true)]
    [InlineData(MembershipRole.Viewer, OrgCapability.ExportData, false)]
    [InlineData(MembershipRole.Viewer, OrgCapability.ManageSharing, false)]
    [InlineData(MembershipRole.Viewer, OrgCapability.ManageBilling, false)]
    [InlineData(MembershipRole.Viewer, OrgCapability.ManageTokens, false)]
    [InlineData(MembershipRole.Viewer, OrgCapability.ManageMembers, false)]
    [InlineData(MembershipRole.Viewer, OrgCapability.ManageNotifications, false)]
    [InlineData(MembershipRole.Viewer, OrgCapability.UseProjects, false)]
    [InlineData(MembershipRole.Viewer, OrgCapability.ViewEvidence, true)]
    public void CapabilityMatrix(MembershipRole role, OrgCapability capability, bool allowed)
    {
        Assert.Equal(allowed, OrgCapabilities.Allows(role, capability));
    }

    private static CurrentOrganizationAccessor AccessorWith(params Claim[] claims)
    {
        var ctx = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "ClerkJwt")) };
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        return new CurrentOrganizationAccessor(accessor);
    }

    [Fact]
    public void RequireReturnsOrgIdForPermittedCapability()
    {
        var org = Guid.NewGuid();
        var guard = AccessorWith(new Claim("org_id", org.ToString()), new Claim("org_role", "Admin"));
        Assert.Equal(org, guard.Require(OrgCapability.ManageTokens));
    }

    [Fact]
    public void RequireThrowsForMemberOnAdminCapability()
    {
        var guard = AccessorWith(new Claim("org_id", Guid.NewGuid().ToString()), new Claim("org_role", "Member"));
        Assert.Throws<ForbiddenException>(() => guard.Require(OrgCapability.ManageTokens));
        Assert.Equal(Guid.Parse(guard.OrganizationId!.Value.ToString()), guard.Require(OrgCapability.UseProjects));
    }

    [Fact]
    public void RequireThrowsWhenNoActiveOrganization()
    {
        var guard = AccessorWith(new Claim("sub", "u1"));
        Assert.Null(guard.OrganizationId);
        Assert.Null(guard.Role);
        Assert.Throws<ForbiddenException>(() => guard.Require(OrgCapability.ViewEvidence));
    }

    [Fact]
    public async Task EnsureNotLastAdminBlocksRemovingTheOnlyAdmin()
    {
        var table = new InMemoryHostedTable();
        var memberships = new MembershipRepository(table);
        var service = new MembershipService(memberships);
        var org = Guid.NewGuid();
        var admin = Guid.NewGuid();
        var member = Guid.NewGuid();
        await memberships.PutAsync(new() { OrganizationId = org, UserId = admin, Role = MembershipRole.Admin, CreatedAt = DateTimeOffset.UtcNow });
        await memberships.PutAsync(new() { OrganizationId = org, UserId = member, Role = MembershipRole.Member, CreatedAt = DateTimeOffset.UtcNow });

        await Assert.ThrowsAsync<ForbiddenException>(() => service.EnsureNotLastAdminAsync(org, admin));

        // Removing a non-admin is fine.
        await service.EnsureNotLastAdminAsync(org, member);

        // With a second admin, removing the first is fine.
        var admin2 = Guid.NewGuid();
        await memberships.PutAsync(new() { OrganizationId = org, UserId = admin2, Role = MembershipRole.Admin, CreatedAt = DateTimeOffset.UtcNow });
        await service.EnsureNotLastAdminAsync(org, admin);
    }

    [Fact]
    public async Task MemberSessionIsForbiddenFromIssuingTokens()
    {
        using var factory = new CustomWebApplicationFactory { UseTestClerkAuth = true };
        var table = factory.Services.GetRequiredService<IHostedTable>();
        var org = new Organization { Id = Guid.NewGuid(), Name = "Acme", CreatedAt = DateTimeOffset.UtcNow };
        await table.PutItemAsync(OrganizationRepository.ToItem(org));
        var project = await new ProjectRepository(table).CreateAsync(org.Id, "web");

        var memberClient = factory.CreateClientForOrg(org.Id, MembershipRole.Member);
        var forbidden = await memberClient.PostAsync($"/api/dashboard/projects/{project.Id}/tokens", null);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var adminClient = factory.CreateClientForOrg(org.Id, MembershipRole.Admin);
        var ok = await adminClient.PostAsync($"/api/dashboard/projects/{project.Id}/tokens", null);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }
}
