using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Data.Store;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

public class ProjectWritabilityServiceTests
{
    private static Project P(string name, int ageDays) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        OrganizationId = Guid.NewGuid(),
        CreatedAt = DateTimeOffset.UtcNow.AddDays(-ageDays),
    };

    [Fact]
    public void NullCapMeansAllWritable()
    {
        var projects = new[] { P("a", 3), P("b", 2), P("c", 1) };
        var writable = ProjectWritabilityService.WritableProjectIds(projects, null);
        Assert.Equal(3, writable.Count);
    }

    [Fact]
    public void OldestProjectsUpToTheCapStayWritable()
    {
        var oldest = P("oldest", 30);
        var middle = P("middle", 20);
        var newest = P("newest", 10);
        var writable = ProjectWritabilityService.WritableProjectIds(new[] { newest, oldest, middle }, 1);

        Assert.Contains(oldest.Id, writable);
        Assert.DoesNotContain(middle.Id, writable);
        Assert.DoesNotContain(newest.Id, writable);
    }

    [Fact]
    public void UnderTheCapNothingIsReadOnly()
    {
        var projects = new[] { P("a", 3), P("b", 2) };
        var writable = ProjectWritabilityService.WritableProjectIds(projects, 5);
        Assert.Equal(2, writable.Count);
    }

    // Scenario: 3 projects on Team → downgrade to Free → oldest writable, other two read-only, all visible; re-upgrade restores all.
    [Fact]
    public async Task DowngradeMakesExcessProjectsReadOnlyThenReUpgradeRestoresThem()
    {
        var table = new InMemoryHostedTable();
        var organizations = new OrganizationRepository(table);
        var projects = new ProjectRepository(table);
        var writability = new ProjectWritabilityService(organizations, projects, TestEntitlements.Service);

        var orgId = Guid.NewGuid();
        await table.PutItemAsync(OrganizationRepository.ToItem(new Organization
        {
            Id = orgId,
            Name = "Acme",
            CreatedAt = DateTimeOffset.UtcNow,
            PlanTier = PlanTier.Team,
        }));

        var p1 = await projects.CreateAsync(orgId, "one");
        await Task.Delay(5);
        var p2 = await projects.CreateAsync(orgId, "two");
        await Task.Delay(5);
        var p3 = await projects.CreateAsync(orgId, "three");

        Assert.True(await writability.IsWritableAsync(orgId, p1.Id));
        Assert.True(await writability.IsWritableAsync(orgId, p3.Id));

        await organizations.SetPlanTierAsync(orgId, PlanTier.Free);

        var writable = await writability.WritableProjectIdsAsync(orgId);
        Assert.Equal(new[] { p1.Id }, writable);
        Assert.False(await writability.IsWritableAsync(orgId, p2.Id));
        Assert.False(await writability.IsWritableAsync(orgId, p3.Id));

        // all three projects remain listed
        Assert.Equal(3, (await projects.ListByOrganizationAsync(orgId)).Count);

        await organizations.SetPlanTierAsync(orgId, PlanTier.Team);

        Assert.Equal(3, (await writability.WritableProjectIdsAsync(orgId)).Count);
    }
}
