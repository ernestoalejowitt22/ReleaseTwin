using Amazon.S3;
using Amazon.S3.Model;

namespace ReleaseTwin.Hosted.Api.Data.Store;

/// <summary>
/// evidence-purge-and-blob-store: the production <see cref="IEvidenceBlobStore"/> — one private S3
/// bucket, one object per redacted screenshot. Selected at composition when <c>Evidence:BlobBucket</c>
/// is configured; the filesystem store stays the default for local dev.
///
/// security-hardening-pre-pilot D3: keys are <c>screenshots/{projectId}/{screenshotId}</c>. Screenshot
/// ids are validated to 32 hex at ingest, and the project prefix means one project's upload can never
/// overwrite another's blob or collide with the <c>exports/</c> prefix in the same bucket.
/// </summary>
public sealed class S3EvidenceBlobStore : IEvidenceBlobStore
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;

    public S3EvidenceBlobStore(IAmazonS3 s3, string bucket)
    {
        _s3 = s3;
        _bucket = bucket;
    }

    private static string NamespacedKey(Guid projectId, string id) => $"screenshots/{projectId:N}/{id}";

    public async Task PutAsync(Guid projectId, string id, byte[] png, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(png, writable: false);
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = NamespacedKey(projectId, id),
            InputStream = stream,
            ContentType = "image/png",
        }, cancellationToken);
    }

    public async Task<byte[]?> GetAsync(Guid projectId, string id, CancellationToken cancellationToken = default)
    {
        return await GetKeyAsync(NamespacedKey(projectId, id), cancellationToken)
            // security-hardening-pre-pilot D3: legacy flat key for evidence stored before
            // project-namespacing. Removable once no flat-key blobs remain (evidence is
            // retention-windowed).
            ?? await GetKeyAsync(id, cancellationToken);
    }

    private async Task<byte[]?> GetKeyAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _s3.GetObjectAsync(_bucket, key, cancellationToken);
            using var buffer = new MemoryStream();
            await response.ResponseStream.CopyToAsync(buffer, cancellationToken);
            return buffer.ToArray();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound || ex.ErrorCode == "NoSuchKey")
        {
            return null;
        }
    }

    public async Task DeleteAsync(Guid projectId, string id, CancellationToken cancellationToken = default)
    {
        // DeleteObject is idempotent — deleting a missing key returns 204, not an error. Clear both the
        // namespaced key and any legacy flat key for the same id.
        await _s3.DeleteObjectAsync(_bucket, NamespacedKey(projectId, id), cancellationToken);
        await _s3.DeleteObjectAsync(_bucket, id, cancellationToken);
    }
}
