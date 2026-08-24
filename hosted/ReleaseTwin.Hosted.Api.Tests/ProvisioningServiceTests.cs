using Microsoft.EntityFrameworkCore;
using ReleaseTwin.Hosted.Api.Data;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

public class ProvisioningServiceTests
{
    private static HostedDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<HostedDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HostedDbContext(options);
    }

    private static ProvisioningService NewService(HostedDbContext db) => new(db, new TokenService());

    // Scenario: New signup is immediately usable
    [Fact]
    public async Task NewSignupIsImmediatelyUsable()
    {
        await using var db = NewDb();
        var service = NewService(db);

        var user = await service.GetOrCreateUserAsync("clerk-user-1", "alice", "alice@example.com");

        Assert.NotEqual(Guid.Empty, user.OrganizationId);
        var org = await db.Organizations.FindAsync(user.OrganizationId);
        Assert.NotNull(org);
    }

    [Fact]
    public async Task RepeatedLoginReturnsSameUserAndOrganization()
    {
        await using var db = NewDb();
        var service = NewService(db);

        var first = await service.GetOrCreateUserAsync("clerk-user-1", "alice", "alice@example.com");
        var second = await service.GetOrCreateUserAsync("clerk-user-1", "alice", "alice@example.com");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.OrganizationId, second.OrganizationId);
    }

    // Scenario: Customer creates their first project
    [Fact]
    public async Task CustomerCreatesTheirFirstProject()
    {
        await using var db = NewDb();
        var service = NewService(db);
        var user = await service.GetOrCreateUserAsync("clerk-user-1", "alice", null);

        var project = await service.CreateProjectAsync(user.OrganizationId, "Claims Portal");

        Assert.Equal(user.OrganizationId, project.OrganizationId);
        Assert.Empty(project.ApiTokens);
    }

    // Scenario: Token is scoped to its own project
    [Fact]
    public async Task TokenIsScopedToItsOwnProject()
    {
        await using var db = NewDb();
        var service = NewService(db);
        var user = await service.GetOrCreateUserAsync("clerk-user-1", "alice", null);
        var projectA = await service.CreateProjectAsync(user.OrganizationId, "A");
        var projectB = await service.CreateProjectAsync(user.OrganizationId, "B");

        var (token, raw) = await service.IssueTokenAsync(projectA.Id);

        Assert.Equal(projectA.Id, token.ProjectId);
        Assert.NotEqual(projectB.Id, token.ProjectId);
        Assert.StartsWith("rtw_", raw);
    }

    // Scenario: Revoked token is rejected
    [Fact]
    public async Task RevokedTokenIsRejected()
    {
        await using var db = NewDb();
        var service = NewService(db);
        var user = await service.GetOrCreateUserAsync("clerk-user-1", "alice", null);
        var project = await service.CreateProjectAsync(user.OrganizationId, "A");
        var (token, _) = await service.IssueTokenAsync(project.Id);

        await service.RevokeTokenAsync(token.Id);

        var reloaded = await db.ApiTokens.FindAsync(token.Id);
        Assert.NotNull(reloaded);
        Assert.True(reloaded!.IsRevoked);
    }
}
