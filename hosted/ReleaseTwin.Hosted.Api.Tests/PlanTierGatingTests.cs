using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

public class PlanTierGatingTests
{
    private static (ProvisioningService Service, IOrganizationRepository Organizations) NewService()
    {
        var table = new InMemoryHostedTable();
        var users = new UserRepository(table);
        var organizations = new OrganizationRepository(table);
        var projects = new ProjectRepository(table);
        var tokens = new ApiTokenRepository(table);
        return (new ProvisioningService(users, organizations, projects, tokens, new TokenService(), TestEntitlements.Service), organizations);
    }

    // Scenario: New organizations start on Free
    [Fact]
    public async Task NewOrganizationsDefaultToFree()
    {
        var (service, organizations) = NewService();

        var user = await service.GetOrCreateUserAsync("clerk-1", "alice", null);

        var org = await organizations.GetAsync(user.OrganizationId);
        Assert.NotNull(org);
        Assert.Equal(PlanTier.Free, org!.PlanTier);
    }

    // Scenario: A Free-tier organization's first project succeeds
    [Fact]
    public async Task FreeTierOrganizationsFirstProjectSucceeds()
    {
        var (service, _) = NewService();
        var user = await service.GetOrCreateUserAsync("clerk-1", "alice", null);

        var project = await service.CreateProjectAsync(user.OrganizationId, "First");

        Assert.Equal(user.OrganizationId, project.OrganizationId);
    }

    // Scenario: A Free-tier organization's second project is rejected
    [Fact]
    public async Task FreeTierOrganizationsSecondProjectIsRejected()
    {
        var (service, _) = NewService();
        var user = await service.GetOrCreateUserAsync("clerk-1", "alice", null);
        await service.CreateProjectAsync(user.OrganizationId, "First");

        await Assert.ThrowsAsync<ProjectLimitExceededException>(
            () => service.CreateProjectAsync(user.OrganizationId, "Second"));
    }

    // Scenario: A Paid-tier organization creates additional projects
    [Fact]
    public async Task PaidTierOrganizationsNthProjectAlwaysSucceeds()
    {
        var (service, _) = NewService();
        var user = await service.GetOrCreateUserAsync("clerk-1", "alice", null);
        await service.UpgradeToTeamAsync(user.OrganizationId);

        await service.CreateProjectAsync(user.OrganizationId, "1");
        await service.CreateProjectAsync(user.OrganizationId, "2");
        var third = await service.CreateProjectAsync(user.OrganizationId, "3");

        Assert.Equal(user.OrganizationId, third.OrganizationId);
    }

    // Scenario: Upgrading lifts the project limit immediately
    [Fact]
    public async Task UpgradingAFreeTierOrganizationAtItsLimitImmediatelyAllowsAnotherProject()
    {
        var (service, _) = NewService();
        var user = await service.GetOrCreateUserAsync("clerk-1", "alice", null);
        await service.CreateProjectAsync(user.OrganizationId, "First");

        await service.UpgradeToTeamAsync(user.OrganizationId);
        var second = await service.CreateProjectAsync(user.OrganizationId, "Second");

        Assert.Equal(user.OrganizationId, second.OrganizationId);
    }

    // Scenario: self-serve upgrade targets Team, never Enterprise
    [Fact]
    public async Task SelfServeUpgradeGoesToTeamNotEnterprise()
    {
        var (service, organizations) = NewService();
        var user = await service.GetOrCreateUserAsync("clerk-1", "alice", null);

        await service.UpgradeToTeamAsync(user.OrganizationId);

        var org = await organizations.GetAsync(user.OrganizationId);
        Assert.Equal(PlanTier.Team, org!.PlanTier);
    }

    // Scenario: an operator can set Enterprise out-of-band
    [Fact]
    public async Task OperatorCanSetEnterpriseTier()
    {
        var (service, organizations) = NewService();
        var user = await service.GetOrCreateUserAsync("clerk-1", "alice", null);

        await service.SetTierAsync(user.OrganizationId, PlanTier.Enterprise);

        var org = await organizations.GetAsync(user.OrganizationId);
        Assert.Equal(PlanTier.Enterprise, org!.PlanTier);
    }

    // Scenario: A previously "Paid" organization reads as Team
    [Fact]
    public void PreviouslyPaidTierReadsAsTeam()
    {
        Assert.Equal(PlanTier.Team, OrganizationRepository.ParsePlanTier("Paid"));
        Assert.Equal(PlanTier.Free, OrganizationRepository.ParsePlanTier(null));
        Assert.Equal(PlanTier.Enterprise, OrganizationRepository.ParsePlanTier("Enterprise"));
        Assert.Equal(PlanTier.Free, OrganizationRepository.ParsePlanTier("something-unknown"));
    }
}
