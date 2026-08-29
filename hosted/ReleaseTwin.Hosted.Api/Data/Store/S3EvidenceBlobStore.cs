using Amazon.S3;
using Amazon.S3.Model;

namespace ReleaseTwin.Hosted.Api.Data.Store;

/// <summary>
/// evidence-purge-and-blob-store: the production <see cref="IEvidenceBlobStore"/> — one private S3
/// bucket, one object per redacted screenshot keyed by its id. Selected at composition when
/// <c>Evidence:BlobBucket</c> is configured; the filesystem store stays the default for local dev.
/// Screenshot ids are already 32 hex chars (see the ingest path), so no key sanitizing is needed.
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

    public async Task PutAsync(string id, byte[] png, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(png, writable: false);
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = id,
            InputStream = stream,
            ContentType = "image/png",
        }, cancellationToken);
    }

    public async Task<byte[]?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _s3.GetObjectAsync(_bucket, id, cancellationToken);
            using var buffer = new MemoryStream();
            await response.ResponseStream.CopyToAsync(buffer, cancellationToken);
            return buffer.ToArray();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound || ex.ErrorCode == "NoSuchKey")
        {
            return null;
        }
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        // DeleteObject is idempotent — deleting a missing key returns 204, not an error.
        await _s3.DeleteObjectAsync(_bucket, id, cancellationToken);
    }
}
