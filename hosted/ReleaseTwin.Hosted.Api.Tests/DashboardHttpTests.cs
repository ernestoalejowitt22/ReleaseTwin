using System.Net;

namespace ReleaseTwin.Hosted.Api.Tests;

public class DashboardHttpTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public DashboardHttpTests(CustomWebApplicationFactory factory) => _factory = factory;

    // Scenario: Unauthenticated access is denied
    [Fact]
    public async Task UnauthenticatedAccessIsDenied()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/dashboard");

        // Unlike the old cookie scheme (which redirected to a challenge URL), the "ClerkJwt" bearer
        // scheme has no such concept for a JSON API — an unauthenticated request is rejected outright.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Scenario: An API token alone does not grant dashboard access — see SchemeIsolationTests.cs for
    // the full "Bearer-shaped credentials aren't interchangeable" coverage.
}
