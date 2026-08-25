using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseTwin.Hosted.Api.Data.Repositories;
using ReleaseTwin.Hosted.Api.Services;

namespace ReleaseTwin.Hosted.Api.Auth;

public static class ApiTokenDefaults
{
    public const string Scheme = "ApiToken";
    public const string ProjectIdClaim = "project_id";
    public const string OrganizationIdClaim = "organization_id";
}

/// <summary>
/// ingest-api spec: "Ingest requires a valid API token" — rejects missing/invalid/revoked tokens
/// before any request processing. Never accepts a web session as a substitute (design.md D3 — two
/// distinct auth domains). usage-metering design.md: `GetByHashAsync` is a strongly-consistent
/// `GetItem` on the table's own primary key (not a GSI), so a just-revoked token is rejected
/// immediately, not after eventual-consistency lag.
/// </summary>
public sealed class ApiTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IApiTokenRepository _tokens;
    private readonly ITokenService _tokenService;

    public ApiTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiTokenRepository tokens,
        ITokenService tokenService)
        : base(options, logger, encoder)
    {
        _tokens = tokens;
        _tokenService = tokenService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var headerValue))
        {
            return AuthenticateResult.Fail("Missing Authorization header.");
        }

        var raw = headerValue.ToString();
        const string bearerPrefix = "Bearer ";
        if (!raw.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.Fail("Authorization header must use the Bearer scheme.");
        }

        var rawToken = raw[bearerPrefix.Length..].Trim();
        if (string.IsNullOrEmpty(rawToken))
        {
            return AuthenticateResult.Fail("Empty bearer token.");
        }

        var hash = _tokenService.Hash(rawToken);
        var token = await _tokens.GetByHashAsync(hash);
        if (token is null || token.IsRevoked)
        {
            return AuthenticateResult.Fail("Invalid or revoked API token.");
        }

        var claims = new[]
        {
            new Claim(ApiTokenDefaults.ProjectIdClaim, token.ProjectId.ToString()),
            new Claim(ApiTokenDefaults.OrganizationIdClaim, token.OrganizationId.ToString()),
        };
        var identity = new ClaimsIdentity(claims, ApiTokenDefaults.Scheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ApiTokenDefaults.Scheme);
        return AuthenticateResult.Success(ticket);
    }
}
