using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace ReleaseTwin.Hosted.Api.Services;

public interface IConnectionStateService
{
    string Mint(Guid projectId);
    Guid? Validate(string state);
}

/// <summary>
/// project-connections design.md: the GitHub connection flow is a hand-rolled OAuth exchange, not
/// ASP.NET Core's AddOAuth/remote-authentication pipeline — so it gets none of that pipeline's
/// built-in CSRF protection for free. This mints a signed, time-limited `state` value (via ASP.NET
/// Core's own DataProtection, not a hand-rolled secret/HMAC config entry) so a callback can't be
/// replayed, tampered with, or aimed at a different project than the one that started the flow.
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

    public string Mint(Guid projectId) => _protector.Protect(projectId.ToString(), _lifetime);

    public Guid? Validate(string state)
    {
        try
        {
            var payload = _protector.Unprotect(state);
            return Guid.TryParse(payload, out var projectId) ? projectId : null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
