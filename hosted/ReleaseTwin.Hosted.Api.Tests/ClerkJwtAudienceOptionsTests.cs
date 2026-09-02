using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ReleaseTwin.Hosted.Api.Tests;

/// <summary>
/// security-hardening-pre-pilot D1: JWT audience validation is wired to the presence of the
/// <c>Clerk:Audience</c> config value — off (issuer + signature + expiry only) when unset, so a
/// deploy that predates the Clerk JWT template keeps working; on and pinned to that value once set.
/// </summary>
public class ClerkJwtAudienceOptionsTests
{
    private static JwtBearerOptions ClerkOptions(CustomWebApplicationFactory factory) =>
        factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("ClerkJwt");

    [Fact]
    public void AudienceValidationIsOffWhenClerkAudienceUnset()
    {
        using var factory = new CustomWebApplicationFactory();

        var options = ClerkOptions(factory);

        Assert.False(options.TokenValidationParameters.ValidateAudience);
    }

    [Fact]
    public void AudienceValidationIsOnAndPinnedWhenClerkAudienceSet()
    {
        using var factory = new CustomWebApplicationFactory();
        // Clerk:Audience is read while the WebApplicationBuilder is constructed, so it must be a host
        // setting rather than an app-configuration source added later.
        using var configured = factory.WithWebHostBuilder(b => b.UseSetting("Clerk:Audience", "releasetwin-hosted-api"));

        var options = configured.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("ClerkJwt");

        Assert.True(options.TokenValidationParameters.ValidateAudience);
        Assert.Equal("releasetwin-hosted-api", options.TokenValidationParameters.ValidAudience);
    }
}
