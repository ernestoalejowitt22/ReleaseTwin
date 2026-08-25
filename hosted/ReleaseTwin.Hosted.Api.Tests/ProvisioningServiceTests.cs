using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

public class ProvisioningServiceTests
{
    private static (ProvisioningService Service, IApiTokenRepository Tokens) NewService()
    {
        var table = new InMemoryHostedTable();
        var users = new UserRepository(table);
        var projects = new ProjectRepository(table);
        var tokens = new ApiTokenRepository(table);
        return (new ProvisioningService(users, projects, tokens, new TokenService()), tokens);
    }

    // Scenario: New signup is immediately usable
    [Fact]
    public async Task NewSignupIsImmediatelyUsable()
    {
        var (service, _) = NewService();

        var user = await service.GetOrCreateUserAsync("clerk-user-1", "alice", "alice@example.com");

        Assert.NotEqual(Guid.Empty, user.OrganizationId);
    }

    [Fact]
    public async Task RepeatedLoginReturnsSameUserAndOrganization()
    {
        var (service, _) = NewService();

        var first = await service.GetOrCreateUserAsync("clerk-user-1", "alice", "alice@example.com");
        var second = await service.GetOrCreateUserAsync("clerk-user-1", "alice", "alice@example.com");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.OrganizationId, second.OrganizationId);
    }

    // Scenario: Customer creates their first project
    [Fact]
    public async Task CustomerCreatesTheirFirstProject()
    {
        var (service, _) = NewService();
        var user = await service.GetOrCreateUserAsync("clerk-user-1", "alice", null);

        var project = await service.CreateProjectAsync(user.OrganizationId, "Claims Portal");

        Assert.Equal(user.OrganizationId, project.OrganizationId);
    }

    // Scenario: Token is scoped to its own project
    [Fact]
    public async Task TokenIsScopedToItsOwnProject()
    {
        var (service, _) = NewService();
        var user = await service.GetOrCreateUserAsync("clerk-user-1", "alice", null);
        var projectA = await service.CreateProjectAsync(user.OrganizationId, "A");
        var projectB = await service.CreateProjectAsync(user.OrganizationId, "B");

        var (token, raw) = await service.IssueTokenAsync(projectA.Id, projectA.OrganizationId);

        Assert.Equal(projectA.Id, token.ProjectId);
        Assert.NotEqual(projectB.Id, token.ProjectId);
        Assert.StartsWith("rtw_", raw);
    }

    // Scenario: Revoked token is rejected
    [Fact]
    public async Task RevokedTokenIsRejected()
    {
        var (service, tokens) = NewService();
        var user = await service.GetOrCreateUserAsync("clerk-user-1", "alice", null);
        var project = await service.CreateProjectAsync(user.OrganizationId, "A");
        var (token, _) = await service.IssueTokenAsync(project.Id, project.OrganizationId);

        await service.RevokeTokenAsync(token.Id);

        var reloaded = await tokens.GetByHashAsync(token.TokenHash);
        Assert.NotNull(reloaded);
        Assert.True(reloaded!.IsRevoked);
    }
}
