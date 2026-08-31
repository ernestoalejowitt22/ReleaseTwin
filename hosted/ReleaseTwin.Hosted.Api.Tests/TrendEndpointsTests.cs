using System.Net;
using ReleaseTwin.Hosted.Api.Analytics;

namespace ReleaseTwin.Hosted.Api.Tests;

public class TrendEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TrendEndpointsTests(CustomWebApplicationFactory factory) => _factory = factory;

    // Scenario: An unentitled organization is refused — the ClerkJwt-only group rejects an
    // unauthenticated request outright (same as every other dashboard endpoint; see DashboardHttpTests).
    [Fact]
    public async Task UnauthenticatedProjectTrendIsRejected()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/projects/{Guid.NewGuid()}/trends?window=30d");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedOrganizationTrendIsRejected()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/trends");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("7d", true, TrendWindow.SevenDays)]
    [InlineData("30d", true, TrendWindow.ThirtyDays)]
    [InlineData("90d", true, TrendWindow.NinetyDays)]
    [InlineData("14d", false, TrendWindow.ThirtyDays)]
    [InlineData("", false, TrendWindow.ThirtyDays)]
    [InlineData(null, false, TrendWindow.ThirtyDays)]
    public void WindowParsingAcceptsOnlyTheThreeValidValues(string? input, bool valid, TrendWindow expected)
    {
        Assert.Equal(valid, TrendWindowParsing.TryParse(input, out var window));
        if (valid)
        {
            Assert.Equal(expected, window);
        }
    }

    [Fact]
    public void FreeTierLacksTrendAnalyticsAndTeamHasIt()
    {
        Assert.False(TestEntitlements.Service.For(ReleaseTwin.Hosted.Api.Data.Entities.PlanTier.Free).TrendAnalytics);
        Assert.True(TestEntitlements.Service.For(ReleaseTwin.Hosted.Api.Data.Entities.PlanTier.Team).TrendAnalytics);
    }
}
