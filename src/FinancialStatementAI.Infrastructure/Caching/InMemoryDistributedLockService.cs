using System.Collections.Concurrent;
using FinancialStatementAI.Application.Interfaces;

namespace FinancialStatementAI.Infrastructure.Caching;

/// <summary>Default IDistributedLockService — guards against concurrent requests within this one
/// process only (not across instances of a horizontally-scaled deployment, unlike
/// RedisDistributedLockService), but zero configuration required. The expiry parameter is
/// accepted for interface parity but has no independent effect here: an in-process lock is
/// already reliably released when its holder's request finishes (whether normally or via an
/// exception), which is the scenario Redis's expiry-as-safety-net exists to approximate when
/// there's no such guarantee across a network.</summary>
public class InMemoryDistributedLockService : IDistributedLockService
{
    private readonly ConcurrentDictionary<string, byte> _heldLocks = new();

    public Task<IAsyncDisposable?> TryAcquireAsync(string resource, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        var acquired = _heldLocks.TryAdd(resource, 0);
        IAsyncDisposable? handle = acquired ? new LockHandle(_heldLocks, resource) : null;
        return Task.FromResult(handle);
    }

    private sealed class LockHandle(ConcurrentDictionary<string, byte> heldLocks, string resource) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            heldLocks.TryRemove(resource, out _);
            return ValueTask.CompletedTask;
        }
    }
}
