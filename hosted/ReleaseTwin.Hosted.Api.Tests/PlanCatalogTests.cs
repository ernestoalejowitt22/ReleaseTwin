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
            Assert.NotEmpty(tier.Prices);
            Assert.All(tier.Prices, p => Assert.False(string.IsNullOrWhiteSpace(p.Unit)));
        }
    }

    [Fact]
    public void Team_offers_monthly_and_annual_cadences_with_annual_cheaper()
    {
        var team = Catalog.Find(PlanTier.Team)!;
        var monthly = team.PriceFor(BillingInterval.Monthly);
        var annual = team.PriceFor(BillingInterval.Annual);
        Assert.NotNull(monthly);
        Assert.NotNull(annual);
        Assert.True(annual!.Amount < monthly!.Amount, "annual per-project price should be lower than monthly");
        Assert.Equal(BillingInterval.Monthly, team.DefaultPrice.Interval);
    }

    [Fact]
    public void Free_offers_a_single_cadence()
    {
        Assert.Single(Catalog.Find(PlanTier.Free)!.Prices);
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
        Assert.All(Catalog.Find(PlanTier.Free)!.Prices, p => Assert.False(p.Placeholder));
        Assert.All(Catalog.Find(PlanTier.Team)!.Prices, p => Assert.True(p.Placeholder));
        Assert.All(Catalog.Find(PlanTier.Enterprise)!.Prices, p => Assert.True(p.Placeholder));
    }
}
