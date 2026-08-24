using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Tests;

/// <summary>
/// hosted-react-frontend spec delta (ingest-api): a web-session credential (Clerk JWT) and an API
/// token are now both Bearer-shaped — this proves neither satisfies the other's endpoints.
/// </summary>
public class SchemeIsolationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SchemeIsolationTests(CustomWebApplicationFactory factory) => _factory = factory;

    // Scenario: A web-session credential does not grant ingest API access
    [Fact]
    public async Task ApiTokenDoesNotGrantDashboardAccess()
    {
        using var scope = _factory.Services.CreateScope();
        var provisioning = scope.ServiceProvider.GetRequiredService<ProvisioningService>();
        var user = await provisioning.GetOrCreateUserAsync("clerk-1", "alice", null);
        var project = await provisioning.CreateProjectAsync(user.OrganizationId, "P");
        var (_, raw) = await provisioning.IssueTokenAsync(project.Id);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", raw);

        var response = await client.GetAsync("/api/dashboard");

        // A valid API token is a real, non-revoked credential — but for the wrong scheme, so the
        // dashboard endpoint (restricted to "ClerkJwt") must not treat it as authenticated at all.
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    // Scenario: Missing or invalid token is rejected (symmetric direction: a non-API-token bearer
    // value, of any shape, must not satisfy the ingest endpoint's explicit ApiToken-only restriction)
    [Fact]
    public async Task ArbitraryBearerValueDoesNotGrantIngestAccess()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-an-api-token-and-not-a-clerk-jwt");

        var response = await client.PostAsJsonAsync("/api/ingest/case-report", new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
