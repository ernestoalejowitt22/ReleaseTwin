using System.Security.Cryptography;
using System.Text;

namespace ReleaseTwin.Hosted.Api.Services;

public sealed record GeneratedToken(string RawValue, string Hash, string DisplayPrefix);

public interface ITokenService
{
    GeneratedToken GenerateToken();
    string Hash(string rawValue);
}

/// <summary>
/// Generates opaque bearer tokens for the ingest API. Only a SHA-256 hash is ever persisted
/// (account-provisioning: tokens are self-serve issued/revoked; ingest-api: token auth).
/// </summary>
public sealed class TokenService : ITokenService
{
    private const string Prefix = "rtw_";

    public GeneratedToken GenerateToken()
    {
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var raw = Prefix + secret;
        return new GeneratedToken(raw, Hash(raw), raw[..(Prefix.Length + 8)]);
    }

    public string Hash(string rawValue) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawValue))).ToLowerInvariant();
}
