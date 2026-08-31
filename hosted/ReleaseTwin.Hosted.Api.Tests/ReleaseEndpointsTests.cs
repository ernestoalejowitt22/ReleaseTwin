using System.Net;
using ReleaseTwin.Hosted.Api.Data.Entities;
using ReleaseTwin.Hosted.Api.Releases;

namespace ReleaseTwin.Hosted.Api.Tests;

public class ReleaseEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ReleaseEndpointsTests(CustomWebApplicationFactory factory) => _factory = factory;

    // The ClerkJwt-only group rejects an unauthenticated request outright (same as every other
    // dashboard endpoint; see DashboardHttpTests). Cross-org and entitlement refusal are enforced
    // by the shared GuardAsync path and covered at the service / catalog level.
    [Fact]
    public async Task UnauthenticatedReleasesListIsRejected()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/projects/{Guid.NewGuid()}/releases");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedRollupIsRejected()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/projects/{Guid.NewGuid()}/releases/4.2?window=14d");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("7d", true, 7)]
    [InlineData("14d", true, 14)]
    [InlineData("30d", true, 30)]
    [InlineData("90d", true, 90)]
    [InlineData("21d", false, 14)]
    [InlineData(null, false, 14)]
    public void WindowParsingAcceptsOnlyTheAllowlist(string? input, bool valid, int expectedDays)
    {
        Assert.Equal(valid, ReleaseWindowParsing.TryParse(input, out var days));
        if (valid)
        {
            Assert.Equal(expectedDays, days);
        }
    }

    [Fact]
    public void FreeTierLacksReleaseRollupAndTeamHasIt()
    {
        Assert.False(TestEntitlements.Service.For(PlanTier.Free).ReleaseRollup);
        Assert.True(TestEntitlements.Service.For(PlanTier.Team).ReleaseRollup);
    }
}
