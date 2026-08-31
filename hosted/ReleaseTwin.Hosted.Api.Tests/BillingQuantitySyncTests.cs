using ReleaseTwin.Hosted.Api.Billing;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

public class BillingQuantitySyncTests
{
    private sealed record Harness(ProvisioningService Service, IProjectRepository Projects, FakePolarClient Polar, Guid OrgId);

    private static async Task<Harness> NewAsync(PlanTier tier, string? subscriptionId)
    {
        var table = new InMemoryHostedTable();
        var organizations = new OrganizationRepository(table);
        var projects = new ProjectRepository(table);
        var polar = new FakePolarClient();
        var service = new ProvisioningService(
            new UserRepository(table), organizations, projects, new ApiTokenRepository(table),
            new TokenService(), TestEntitlements.Service, polar);

        var orgId = Guid.NewGuid();
        var org = new Organization { Id = orgId, Name = "Acme", CreatedAt = DateTimeOffset.UtcNow, PlanTier = tier };
        await table.PutItemAsync(OrganizationRepository.ToItem(org));
        if (subscriptionId is not null)
        {
            await organizations.SetBillingAsync(orgId, BillingStatus.Active, DateTimeOffset.UtcNow, BillingCadence.Monthly, "cus_1", subscriptionId);
        }

        return new Harness(service, projects, polar, orgId);
    }

    [Fact]
    public async Task PaidOrgCreateBumpsQuantityThenCreates()
    {
        var h = await NewAsync(PlanTier.Team, "sub_1");
        await h.Service.CreateProjectAsync(h.OrgId, "one");
        await h.Service.CreateProjectAsync(h.OrgId, "two");

        Assert.Equal(new[] { 1, 2 }, h.Polar.QuantityUpdates.Select(q => q.Quantity));
        Assert.Equal(2, (await h.Projects.ListByOrganizationAsync(h.OrgId)).Count);
    }

    [Fact]
    public async Task PolarRejectionBlocksProjectCreation()
    {
        var h = await NewAsync(PlanTier.Team, "sub_1");
        h.Polar.FailNextQuantityUpdate = new PolarException("card declined");

        await Assert.ThrowsAsync<EntitlementRequiredException>(() => h.Service.CreateProjectAsync(h.OrgId, "one"));
        Assert.Empty(await h.Projects.ListByOrganizationAsync(h.OrgId));
    }

    [Fact]
    public async Task DeleteLowersQuantity()
    {
        var h = await NewAsync(PlanTier.Team, "sub_1");
        var p1 = await h.Service.CreateProjectAsync(h.OrgId, "one");
        await h.Service.CreateProjectAsync(h.OrgId, "two");
        h.Polar.QuantityUpdates.Clear();

        await h.Service.DeleteProjectAsync(h.OrgId, p1.Id);

        Assert.Equal(1, h.Polar.QuantityUpdates.Single().Quantity);
        Assert.Single(await h.Projects.ListByOrganizationAsync(h.OrgId));
    }

    [Fact]
    public async Task DeleteWithPolarFailureStillDeletes()
    {
        var h = await NewAsync(PlanTier.Team, "sub_1");
        var p1 = await h.Service.CreateProjectAsync(h.OrgId, "one");
        h.Polar.FailNextQuantityUpdate = new PolarException("polar down");

        await h.Service.DeleteProjectAsync(h.OrgId, p1.Id);

        Assert.Empty(await h.Projects.ListByOrganizationAsync(h.OrgId));
    }

    [Fact]
    public async Task OperatorEnterpriseOrgIsNotBilledForProjects()
    {
        var h = await NewAsync(PlanTier.Enterprise, subscriptionId: null);
        var p1 = await h.Service.CreateProjectAsync(h.OrgId, "one");
        await h.Service.DeleteProjectAsync(h.OrgId, p1.Id);

        Assert.Empty(h.Polar.QuantityUpdates);
    }
}
