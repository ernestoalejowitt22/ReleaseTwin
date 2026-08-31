using Microsoft.Extensions.Logging.Abstractions;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Plans;

namespace ReleaseTwin.Hosted.Api.Tests;

public class EntitlementServiceTests
{
    private static readonly IEntitlementService Service =
        new EntitlementService(PlanCatalog.Load(), NullLogger<EntitlementService>.Instance);

    [Fact]
    public void Free_organization_resolves_to_free_entitlements()
    {
        var ent = Service.For(new Organization { Name = "o", PlanTier = PlanTier.Free });
        Assert.Equal(1, ent.MaxProjects);
        Assert.False(ent.EvidenceViewer);
    }

    [Fact]
    public void Team_organization_resolves_to_team_entitlements()
    {
        var ent = Service.For(new Organization { Name = "o", PlanTier = PlanTier.Team });
        Assert.Null(ent.MaxProjects);
        Assert.True(ent.EvidenceViewer);
    }

    [Fact]
    public void Null_organization_resolves_to_free()
    {
        var ent = Service.For((Organization?)null);
        Assert.Equal(1, ent.MaxProjects);
    }

    [Fact]
    public void An_unknown_stored_tier_degrades_to_free_rather_than_throwing()
    {
        var ent = Service.For((PlanTier)999);
        Assert.Equal(Service.For(PlanTier.Free), ent);
    }
}
