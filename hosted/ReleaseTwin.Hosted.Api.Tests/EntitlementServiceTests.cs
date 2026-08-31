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
    public void RunNotifications_and_evidence_sharing_are_denied_on_free_granted_on_team()
    {
        var free = Service.For(new Organization { Name = "o", PlanTier = PlanTier.Free });
        Assert.False(free.RunNotifications);
        Assert.False(free.EvidenceSharing);

        var team = Service.For(new Organization { Name = "o", PlanTier = PlanTier.Team });
        Assert.True(team.RunNotifications);
        Assert.True(team.EvidenceSharing);

        var enterprise = Service.For(new Organization { Name = "o", PlanTier = PlanTier.Enterprise });
        Assert.True(enterprise.RunNotifications);
        Assert.True(enterprise.EvidenceSharing);
    }

    [Fact]
    public void Losing_the_team_tier_revokes_run_notifications_and_evidence_sharing()
    {
        // A Team org that has been canceled degrades to Free entitlements immediately.
        var canceled = Service.For(Team(BillingStatus.Canceled, DateTimeOffset.UtcNow.AddDays(-1)));
        Assert.False(canceled.RunNotifications);
        Assert.False(canceled.EvidenceSharing);
    }

    [Fact]
    public void An_unknown_stored_tier_degrades_to_free_rather_than_throwing()
    {
        var ent = Service.For((PlanTier)999);
        Assert.Equal(Service.For(PlanTier.Free), ent);
    }

    private static Organization Team(BillingStatus status, DateTimeOffset since) => new()
    {
        Name = "o",
        PlanTier = PlanTier.Team,
        BillingStatus = status,
        BillingStatusSince = since,
    };

    [Fact]
    public void PastDue_inside_the_grace_window_keeps_full_tier_entitlements()
    {
        var ent = Service.For(Team(BillingStatus.PastDue, DateTimeOffset.UtcNow.AddDays(-13)));
        Assert.Null(ent.MaxProjects);
        Assert.True(ent.EvidenceViewer);
    }

    [Fact]
    public void PastDue_past_the_grace_window_drops_to_free_entitlements()
    {
        var ent = Service.For(Team(BillingStatus.PastDue, DateTimeOffset.UtcNow.AddDays(-15)));
        Assert.Equal(Service.For(PlanTier.Free), ent);
    }

    [Fact]
    public void Recovery_to_active_restores_full_tier_entitlements()
    {
        var ent = Service.For(Team(BillingStatus.Active, DateTimeOffset.UtcNow.AddDays(-30)));
        Assert.Null(ent.MaxProjects);
    }

    [Fact]
    public void Canceled_drops_to_free_entitlements_immediately()
    {
        var ent = Service.For(Team(BillingStatus.Canceled, DateTimeOffset.UtcNow));
        Assert.Equal(Service.For(PlanTier.Free), ent);
    }

    [Fact]
    public void Operator_enterprise_org_is_unaffected_by_billing_status()
    {
        var org = new Organization
        {
            Name = "o",
            PlanTier = PlanTier.Enterprise,
            BillingStatus = BillingStatus.Active,
            BillingStatusSince = DateTimeOffset.UtcNow,
        };
        Assert.Equal(Service.For(PlanTier.Enterprise), Service.For(org));
    }
}
