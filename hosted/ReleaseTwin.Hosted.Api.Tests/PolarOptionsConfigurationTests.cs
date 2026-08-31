using Microsoft.Extensions.Configuration;
using ReleaseTwin.Hosted.Api.Billing;
using ReleaseTwin.Hosted.Api.Data.Entities;

namespace ReleaseTwin.Hosted.Api.Tests;

public class PolarOptionsConfigurationTests
{
    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    // Mirrors how deploy-hosted.yml sets the Lambda env: Polar__ProductIds__Team__Monthly → Polar:ProductIds:Team:Monthly
    private static readonly Dictionary<string, string?> FullConfig = new()
    {
        ["Polar:ApiToken"] = "polar_at_xxx",
        ["Polar:WebhookSecret"] = "whsec_xxx",
        ["Polar:ApiBaseUrl"] = "https://sandbox-api.polar.sh",
        ["Polar:ProductIds:Team:Monthly"] = "prod_monthly_id",
        ["Polar:ProductIds:Team:Annual"] = "prod_annual_id",
        ["Polar:UpgradeEnabled"] = "true",
    };

    [Fact]
    public void NestedProductIdsBindToFlatKeys()
    {
        var options = PolarOptions.FromConfiguration(Config(FullConfig));

        Assert.Equal("prod_monthly_id", options.ProductIdFor(PlanTier.Team, BillingCadence.Monthly));
        Assert.Equal("prod_annual_id", options.ProductIdFor(PlanTier.Team, BillingCadence.Annual));
    }

    [Fact]
    public void FullConfigIsConfiguredAndUpgradeEnabled()
    {
        var options = PolarOptions.FromConfiguration(Config(FullConfig));

        Assert.True(options.IsConfigured);
        Assert.True(options.IsUpgradeEnabled);
        Assert.Equal("https://sandbox-api.polar.sh", options.ApiBaseUrl);
    }

    [Fact]
    public void MissingProductIdsMeansNotConfigured()
    {
        var options = PolarOptions.FromConfiguration(Config(new()
        {
            ["Polar:ApiToken"] = "polar_at_xxx",
            ["Polar:WebhookSecret"] = "whsec_xxx",
        }));

        Assert.False(options.IsConfigured);
    }

    [Fact]
    public void EmptyConfigDefaults()
    {
        var options = PolarOptions.FromConfiguration(Config(new()));

        Assert.False(options.IsConfigured);
        Assert.False(options.IsUpgradeEnabled);
        Assert.True(options.ReconciliationDryRun); // safe default
        Assert.Equal("https://api.polar.sh", options.ApiBaseUrl);
    }

    [Fact]
    public void ReconciliationDryRunDefaultsTrueButRespectsFalse()
    {
        Assert.False(PolarOptions.FromConfiguration(Config(new() { ["Polar:ReconciliationDryRun"] = "false" })).ReconciliationDryRun);
        Assert.True(PolarOptions.FromConfiguration(Config(new() { ["Polar:ReconciliationDryRun"] = "true" })).ReconciliationDryRun);
    }
}
