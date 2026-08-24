using System.Collections.Concurrent;

namespace ReleaseTwin.Core;

public interface IResourceSerializer
{
    Task<IDisposable> AcquireAsync(ResourceKey key, CancellationToken cancellationToken);
}

/// <summary>In-process resource-key serialization: cases sharing a resource key never run concurrently against it.</summary>
public sealed class SemaphoreResourceSerializer : IResourceSerializer
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public async Task<IDisposable> AcquireAsync(ResourceKey key, CancellationToken cancellationToken)
    {
        var semaphore = _locks.GetOrAdd(key.Value, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        return new Release(semaphore);
    }

    private sealed class Release : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private bool _disposed;

        public Release(SemaphoreSlim semaphore) => _semaphore = semaphore;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _semaphore.Release();
        }
    }
}
