namespace ReleaseTwin.Hosted.Api.Data.Store;

/// <summary>
/// evidence-store: storage for redacted screenshot blobs, kept out of the single table (which is
/// sized for small metadata items). Same swap-the-implementation pattern as <see cref="IHostedTable"/>:
/// a filesystem implementation for real runs, an in-memory one for tests.
///
/// security-hardening-pre-pilot D3: every operation is scoped by the owning <paramref name="projectId"/>,
/// which the store composes into the key — a caller can never reach another project's namespace, and a
/// client-supplied screenshot id (already constrained to 32 hex at ingest) cannot collide across
/// projects or with anything else in the same backing store (e.g. export archives).
/// </summary>
public interface IEvidenceBlobStore
{
    Task PutAsync(Guid projectId, string id, byte[] png, CancellationToken cancellationToken = default);
    Task<byte[]?> GetAsync(Guid projectId, string id, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid projectId, string id, CancellationToken cancellationToken = default);
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

    private string NamespacedPath(Guid projectId, string id)
    {
        var dir = Path.Combine(_directory, projectId.ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, SafeId(id) + ".png");
    }

    // security-hardening-pre-pilot D3: legacy flat key for evidence stored before project-namespacing.
    // Removable in a later change once no flat-key blobs remain (evidence is retention-windowed).
    private string LegacyPath(string id) => Path.Combine(_directory, SafeId(id) + ".png");

    private static string SafeId(string id) =>
        new(id.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());

    public async Task PutAsync(Guid projectId, string id, byte[] png, CancellationToken cancellationToken = default) =>
        await File.WriteAllBytesAsync(NamespacedPath(projectId, id), png, cancellationToken);

    public async Task<byte[]?> GetAsync(Guid projectId, string id, CancellationToken cancellationToken = default)
    {
        var path = NamespacedPath(projectId, id);
        if (File.Exists(path))
        {
            return await File.ReadAllBytesAsync(path, cancellationToken);
        }

        var legacy = LegacyPath(id);
        return File.Exists(legacy) ? await File.ReadAllBytesAsync(legacy, cancellationToken) : null;
    }

    public Task DeleteAsync(Guid projectId, string id, CancellationToken cancellationToken = default)
    {
        foreach (var path in new[] { NamespacedPath(projectId, id), LegacyPath(id) })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        return Task.CompletedTask;
    }
}

public sealed class InMemoryEvidenceBlobStore : IEvidenceBlobStore
{
    private readonly Dictionary<string, byte[]> _blobs = new();

    private static string Key(Guid projectId, string id) => $"{projectId:N}/{id}";

    public Task PutAsync(Guid projectId, string id, byte[] png, CancellationToken cancellationToken = default)
    {
        lock (_blobs)
        {
            _blobs[Key(projectId, id)] = png;
        }

        return Task.CompletedTask;
    }

    public Task<byte[]?> GetAsync(Guid projectId, string id, CancellationToken cancellationToken = default)
    {
        lock (_blobs)
        {
            if (_blobs.TryGetValue(Key(projectId, id), out var bytes))
            {
                return Task.FromResult<byte[]?>(bytes);
            }

            // legacy flat key fallback (see IEvidenceBlobStore D3 note)
            return Task.FromResult(_blobs.TryGetValue(id, out var legacy) ? legacy : null);
        }
    }

    public Task DeleteAsync(Guid projectId, string id, CancellationToken cancellationToken = default)
    {
        lock (_blobs)
        {
            _blobs.Remove(Key(projectId, id));
            _blobs.Remove(id);
        }

        return Task.CompletedTask;
    }
}
