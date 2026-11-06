namespace Sentinel.Conversations;

using System.Collections.Concurrent;

public sealed class ConversationLocks
{
    private readonly ConcurrentDictionary<ConversationKey, SemaphoreSlim> _locks = new();

    public async Task<IDisposable> AcquireAsync(ConversationKey key, CancellationToken cancellationToken)
    {
        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        return new Releaser(semaphore);
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        public void Dispose() => semaphore.Release();
    }
}
