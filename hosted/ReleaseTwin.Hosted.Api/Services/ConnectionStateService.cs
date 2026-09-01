using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace ReleaseTwin.Hosted.Api.Services;

public interface IConnectionStateService
{
    string Mint(Guid projectId, Guid userId);
    (Guid ProjectId, Guid UserId)? Validate(string state);
}

/// <summary>
/// project-connections design.md: the GitHub connection flow is a hand-rolled OAuth exchange, not
/// ASP.NET Core's AddOAuth/remote-authentication pipeline — so it gets none of that pipeline's
/// built-in CSRF protection for free. This mints a signed, time-limited `state` value (via ASP.NET
/// Core's own DataProtection, not a hand-rolled secret/HMAC config entry) so a callback can't be
/// replayed, tampered with, or aimed at a different project than the one that started the flow.
///
/// security-hardening-pre-pilot D6: the payload also carries the id of the user who started the flow,
/// so the callback can reject a `state` minted for someone else — a link forwarded to another
/// signed-in user cannot complete the flow against the initiator's project.
/// </summary>
public sealed class ConnectionStateService : IConnectionStateService
{
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(10);

    private readonly ITimeLimitedDataProtector _protector;
    private readonly TimeSpan _lifetime;

    public ConnectionStateService(IDataProtectionProvider dataProtectionProvider, TimeSpan? lifetime = null)
    {
        _protector = dataProtectionProvider.CreateProtector("ReleaseTwin.ProjectConnections.State").ToTimeLimitedDataProtector();
        _lifetime = lifetime ?? DefaultLifetime;
    }

    public string Mint(Guid projectId, Guid userId) => _protector.Protect($"{projectId}:{userId}", _lifetime);

    public (Guid ProjectId, Guid UserId)? Validate(string state)
    {
        try
        {
            var parts = _protector.Unprotect(state).Split(':', 2);
            return parts.Length == 2 && Guid.TryParse(parts[0], out var projectId) && Guid.TryParse(parts[1], out var userId)
                ? (projectId, userId)
                : null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
