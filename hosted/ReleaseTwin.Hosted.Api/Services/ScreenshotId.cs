using System.Text.RegularExpressions;

namespace ReleaseTwin.Hosted.Api.Services;

/// <summary>
/// security-hardening-pre-pilot D3: the one place the screenshot-id shape is defined. A screenshot id
/// is a lowercase 32-character hex string — exactly what the CLI emits (<c>Guid.NewGuid().ToString("N")</c>).
/// The ingest path rejects anything else before a blob is written, so downstream code (the blob-store
/// key composition, the share/purge paths) can treat the id as opaque and trusted.
/// </summary>
public static partial class ScreenshotId
{
    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex Pattern();

    public static bool IsValid(string? id) => id is not null && Pattern().IsMatch(id);
}
