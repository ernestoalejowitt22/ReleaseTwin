using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;

namespace ReleaseTwin.Hosted.Api.Tests;

public class OrganizationBillingRepositoryTests
{
    private static (OrganizationRepository Repo, InMemoryHostedTable Table) New()
    {
        var table = new InMemoryHostedTable();
        return (new OrganizationRepository(table), table);
    }

    [Fact]
    public async Task SetBillingRoundTrips()
    {
        var (repo, table) = New();
        var org = new Organization { Id = Guid.NewGuid(), Name = "Acme", CreatedAt = DateTimeOffset.UtcNow };
        await table.PutItemAsync(OrganizationRepository.ToItem(org));

        var since = DateTimeOffset.UtcNow.AddDays(-2);
        await repo.SetBillingAsync(org.Id, BillingStatus.PastDue, since, BillingCadence.Annual, "cus_1", "sub_1");

        var read = await repo.GetAsync(org.Id);
        Assert.NotNull(read);
        Assert.Equal(BillingStatus.PastDue, read!.BillingStatus);
        Assert.Equal(since, read.BillingStatusSince);
        Assert.Equal(BillingCadence.Annual, read.BillingCadence);
        Assert.Equal("cus_1", read.PolarCustomerId);
        Assert.Equal("sub_1", read.PolarSubscriptionId);
    }

    [Fact]
    public async Task LegacyRowWithoutBillingAttributesReadsAsUnlinkedActive()
    {
        var (repo, table) = New();
        var id = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow.AddMonths(-6);
        await table.PutItemAsync(new()
        {
            ["PK"] = Attrs.S(Keys.Org(id)),
            ["SK"] = Attrs.S(Keys.Org(id)),
            ["EntityType"] = Attrs.S("Organization"),
            ["Id"] = Attrs.S(id.ToString()),
            ["Name"] = Attrs.S("Legacy"),
            ["CreatedAt"] = Attrs.S(createdAt.ToString("O")),
            ["PlanTier"] = Attrs.S("Team"),
        });

        var read = await repo.GetAsync(id);
        Assert.NotNull(read);
        Assert.Equal(BillingStatus.Active, read!.BillingStatus);
        Assert.Equal(createdAt, read.BillingStatusSince);
        Assert.Null(read.BillingCadence);
        Assert.Null(read.PolarCustomerId);
        Assert.Null(read.PolarSubscriptionId);
    }

    [Fact]
    public async Task SetBillingCanClearLinkage()
    {
        var (repo, table) = New();
        var org = new Organization { Id = Guid.NewGuid(), Name = "Acme", CreatedAt = DateTimeOffset.UtcNow };
        await table.PutItemAsync(OrganizationRepository.ToItem(org));

        var since = DateTimeOffset.UtcNow;
        await repo.SetBillingAsync(org.Id, BillingStatus.Active, since, BillingCadence.Monthly, "cus_1", "sub_1");
        await repo.SetBillingAsync(org.Id, BillingStatus.Canceled, since, null, null, null);

        var read = await repo.GetAsync(org.Id);
        Assert.Equal(BillingStatus.Canceled, read!.BillingStatus);
        Assert.Null(read.BillingCadence);
        Assert.Null(read.PolarCustomerId);
        Assert.Null(read.PolarSubscriptionId);
    }
}
