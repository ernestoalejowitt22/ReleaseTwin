namespace ReleaseTwin.Hosted.Api.Data.Store;

/// <summary>
/// evidence-store: storage for redacted screenshot blobs, kept out of the single table (which is
/// sized for small metadata items). Same swap-the-implementation pattern as <see cref="IHostedTable"/>:
/// a filesystem implementation for real runs, an in-memory one for tests.
/// </summary>
public interface IEvidenceBlobStore
{
    Task PutAsync(string id, byte[] png, CancellationToken cancellationToken = default);
    Task<byte[]?> GetAsync(string id, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}

/// <summary>Filesystem-backed blob store. The directory is configurable; defaults under the system temp path.</summary>
public sealed class FileSystemEvidenceBlobStore : IEvidenceBlobStore
{
    private readonly string _directory;

    public FileSystemEvidenceBlobStore(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(_directory);
    }

    private string PathFor(string id) => Path.Combine(_directory, SafeId(id) + ".png");

    private static string SafeId(string id) =>
        new(id.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());

    public async Task PutAsync(string id, byte[] png, CancellationToken cancellationToken = default) =>
        await File.WriteAllBytesAsync(PathFor(id), png, cancellationToken);

    public async Task<byte[]?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var path = PathFor(id);
        return File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken) : null;
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var path = PathFor(id);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }
}

public sealed class InMemoryEvidenceBlobStore : IEvidenceBlobStore
{
    private readonly Dictionary<string, byte[]> _blobs = new();

    public Task PutAsync(string id, byte[] png, CancellationToken cancellationToken = default)
    {
        lock (_blobs)
        {
            _blobs[id] = png;
        }

        return Task.CompletedTask;
    }

    public Task<byte[]?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        lock (_blobs)
        {
            return Task.FromResult(_blobs.TryGetValue(id, out var bytes) ? bytes : null);
        }
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        lock (_blobs)
        {
            _blobs.Remove(id);
        }

        return Task.CompletedTask;
    }
}
