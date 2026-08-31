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

    /// <summary>org-membership: the caller's role in the org. Defaults to <c>Admin</c> so existing tests
    /// (which act as the org owner) keep passing; set to <c>Member</c> to exercise the role gate.</summary>
    public const string RoleHeader = "X-Test-Role";

    /// <summary>org-membership: the Clerk <c>sub</c>. Set this (without <see cref="OrgHeader"/>) for an
    /// authenticated session that has no active organization yet — an invitee before they join.</summary>
    public const string SubHeader = "X-Test-Sub";

    public TestClerkAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        Guid orgId = default;
        var hasOrg = Request.Headers.TryGetValue(OrgHeader, out var orgValues) && Guid.TryParse(orgValues.ToString(), out orgId);
        var subHeader = Request.Headers.TryGetValue(SubHeader, out var subValues) && subValues.ToString() is { Length: > 0 } s ? s : null;

        // org-membership: an authenticated session with NO active org (an invitee who has not joined
        // an org yet) is expressible with X-Test-Sub alone.
        if (!hasOrg && subHeader is null)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userId = Request.Headers.TryGetValue(UserHeader, out var userValues) && Guid.TryParse(userValues.ToString(), out var parsed)
            ? parsed
            : Guid.NewGuid();

        var role = Request.Headers.TryGetValue(RoleHeader, out var roleValues) && roleValues.ToString() is { Length: > 0 } r
            ? r
            : "Admin";

        var claims = new List<Claim>
        {
            new("sub", subHeader ?? $"test-clerk-{(hasOrg ? orgId : userId)}"),
            new("user_id", userId.ToString()),
            new("user_display_name", "Test User"),
        };
        if (hasOrg)
        {
            claims.Add(new Claim("org_id", orgId.ToString()));
            claims.Add(new Claim("org_role", role));
        }

        var identity = new ClaimsIdentity(claims, authenticationType: "ClerkJwt");

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
