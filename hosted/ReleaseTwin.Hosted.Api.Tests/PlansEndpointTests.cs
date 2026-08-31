using System.Net;
using System.Text.Json;

namespace ReleaseTwin.Hosted.Api.Tests;

public class PlansEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PlansEndpointTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Plans_is_readable_without_authentication()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/plans");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var tiers = doc.RootElement.GetProperty("tiers");
        Assert.Equal(3, tiers.GetArrayLength());
        Assert.Equal("free", tiers[0].GetProperty("id").GetString());
        Assert.Equal("enterprise", tiers[2].GetProperty("id").GetString());
        Assert.True(tiers[1].GetProperty("entitlements").GetProperty("evidenceViewer").GetBoolean());
    }

    [Fact]
    public async Task Plans_response_is_cacheable_and_carries_no_caller_data()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/plans");

        Assert.NotNull(response.Headers.CacheControl);
        Assert.True(response.Headers.CacheControl!.Public);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("organization", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("currentTier", body, StringComparison.OrdinalIgnoreCase);
    }
}
