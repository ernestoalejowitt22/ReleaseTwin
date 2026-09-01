using Amazon.S3;
using Amazon.S3.Model;

namespace ReleaseTwin.Hosted.Api.Services.DataExport;

/// <summary>
/// data-export (design D2/D3): where a built export ZIP is put so the browser can download it without
/// pulling the bytes back through the Lambda. Returns a URL the caller redirects to, or null when no
/// store is configured (dev / tests) — in which case the endpoint streams the ZIP in the response.
/// </summary>
public interface IExportArchiveStore
{
    Task<ExportDownload?> StoreAsync(byte[] zip, string fileName, CancellationToken cancellationToken = default);
}

public sealed record ExportDownload(string DownloadUrl, DateTimeOffset ExpiresAt);

/// <summary>data-export: PUTs the archive under <c>exports/&lt;orgId&gt;/&lt;timestamp&gt;.zip</c> in the
/// evidence bucket and hands back a short-lived presigned GET URL. An S3 lifecycle rule expires the
/// <c>exports/</c> prefix (design D5); the 1-hour URL expiry is the real access control.</summary>
public sealed class S3ExportArchiveStore : IExportArchiveStore
{
    public static readonly TimeSpan UrlLifetime = TimeSpan.FromHours(1);

    private readonly IAmazonS3 _s3;
    private readonly string _bucket;

    public S3ExportArchiveStore(IAmazonS3 s3, string bucket)
    {
        _s3 = s3;
        _bucket = bucket;
    }

    public async Task<ExportDownload?> StoreAsync(byte[] zip, string fileName, CancellationToken cancellationToken = default)
    {
        var key = $"exports/{fileName}";

        using var stream = new MemoryStream(zip, writable: false);
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = stream,
            ContentType = "application/zip",
        }, cancellationToken);

        var expiresAt = DateTimeOffset.UtcNow.Add(UrlLifetime);
        var url = await _s3.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = expiresAt.UtcDateTime,
            ResponseHeaderOverrides = { ContentDisposition = $"attachment; filename=\"{Path.GetFileName(fileName)}\"" },
        });

        return new ExportDownload(url, expiresAt);
    }
}

/// <summary>data-export: the dev / test fallback — no archive store, so the endpoint streams the ZIP directly.</summary>
public sealed class NullExportArchiveStore : IExportArchiveStore
{
    public Task<ExportDownload?> StoreAsync(byte[] zip, string fileName, CancellationToken cancellationToken = default) =>
        Task.FromResult<ExportDownload?>(null);
}
