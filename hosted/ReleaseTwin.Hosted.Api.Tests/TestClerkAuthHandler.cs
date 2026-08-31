using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ReleaseTwin.Hosted.Api.Tests;

/// <summary>
/// Test-only stand-in for the real "ClerkJwt" JWT-bearer scheme. A request carrying an
/// <c>X-Test-Org</c> header (a ReleaseTwin organization id) is treated as an authenticated web
/// session for that org — the same <c>org_id</c> / <c>user_id</c> / <c>sub</c> claims the real
/// <c>OnTokenValidated</c> handler adds, which <see cref="ReleaseTwin.Hosted.Api.Services.CurrentOrganizationAccessor"/>
/// and the dashboard endpoints read. No header ⇒ <see cref="AuthenticateResult.NoResult"/>, so the
/// existing "unauthenticated ⇒ 401" tests still hold. Wired up by <see cref="CustomWebApplicationFactory"/>
/// only when <see cref="CustomWebApplicationFactory.UseTestClerkAuth"/> is set.
/// </summary>
public sealed class TestClerkAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string OrgHeader = "X-Test-Org";
    public const string UserHeader = "X-Test-User";

    public TestClerkAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(OrgHeader, out var orgValues) || !Guid.TryParse(orgValues.ToString(), out var orgId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userId = Request.Headers.TryGetValue(UserHeader, out var userValues) && Guid.TryParse(userValues.ToString(), out var parsed)
            ? parsed
            : Guid.NewGuid();

        var identity = new ClaimsIdentity(
            [
                new Claim("sub", $"test-clerk-{orgId}"),
                new Claim("org_id", orgId.ToString()),
                new Claim("user_id", userId.ToString()),
                new Claim("user_display_name", "Test User"),
            ],
            authenticationType: "ClerkJwt");

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
