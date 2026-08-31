using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Plans;

namespace ReleaseTwin.Hosted.Api.Tests;

public class PlanCatalogTests
{
    private static readonly PlanCatalog Catalog = PlanCatalog.Load();

    [Fact]
    public void Catalog_loads_from_the_embedded_plans_json()
    {
        Assert.NotEmpty(Catalog.Tiers);
    }

    [Fact]
    public void Catalog_defines_exactly_three_tiers_in_order()
    {
        Assert.Equal(new[] { "free", "team", "enterprise" }, Catalog.Tiers.Select(t => t.Id).ToArray());
    }

    [Fact]
    public void Every_tier_has_a_complete_entitlement_set()
    {
        foreach (var tier in Catalog.Tiers)
        {
            Assert.NotNull(tier.Entitlements);
            Assert.False(string.IsNullOrWhiteSpace(tier.Name));
            Assert.False(string.IsNullOrWhiteSpace(tier.Support));
            Assert.NotNull(tier.Price);
        }
    }

    [Fact]
    public void Free_tier_has_the_expected_entitlements()
    {
        var free = Catalog.Find(PlanTier.Free)!.Entitlements;
        Assert.Equal(1, free.MaxProjects);
        Assert.Equal(30, free.MaxEvidenceRetentionDays);
        Assert.False(free.EvidenceViewer);
        Assert.False(free.ProjectSecrets);
        Assert.False(free.TrendAnalytics);
        Assert.False(free.ReleaseRollup);
    }

    [Fact]
    public void Team_tier_lifts_the_project_and_evidence_limits()
    {
        var team = Catalog.Find(PlanTier.Team)!.Entitlements;
        Assert.Null(team.MaxProjects);
        Assert.Equal(365, team.MaxEvidenceRetentionDays);
        Assert.True(team.EvidenceViewer);
        Assert.True(team.ProjectSecrets);
        Assert.True(team.TrendAnalytics);
        Assert.True(team.ReleaseRollup);
        Assert.False(team.Sso);
    }

    [Fact]
    public void Enterprise_tier_adds_sso_and_audit_log_and_custom_retention()
    {
        var enterprise = Catalog.Find(PlanTier.Enterprise)!.Entitlements;
        Assert.Null(enterprise.MaxEvidenceRetentionDays);
        Assert.True(enterprise.Sso);
        Assert.True(enterprise.AuditLog);
    }

    [Fact]
    public void Team_and_enterprise_prices_are_flagged_as_placeholders()
    {
        Assert.False(Catalog.Find(PlanTier.Free)!.Price.Placeholder);
        Assert.True(Catalog.Find(PlanTier.Team)!.Price.Placeholder);
        Assert.True(Catalog.Find(PlanTier.Enterprise)!.Price.Placeholder);
    }
}
