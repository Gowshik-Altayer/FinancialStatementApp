namespace FinancialStatementAI.Application.Interfaces;

/// <summary>Abstracts mutual exclusion behind a swappable provider (Phase 15): a zero-config
/// in-process default (guards against concurrent requests within a single instance only) and a
/// real Redis-backed implementation (guards across every instance of a horizontally-scaled
/// deployment) selected via "Caching:Provider" = "Redis" — the same switch as ICacheService,
/// since both need the same Redis connection when enabled.</summary>
public interface IDistributedLockService
{
    /// <summary>Attempts to acquire an exclusive lock on <paramref name="resource"/>. Returns
    /// null immediately if it's already held (never blocks/waits for it to free up — the caller
    /// decides what "someone else is already doing this" means for their own use case). The
    /// returned handle releases the lock when disposed; <paramref name="expiry"/> is a safety net
    /// that releases it automatically even if the holder crashes without disposing it.</summary>
    Task<IAsyncDisposable?> TryAcquireAsync(string resource, TimeSpan expiry, CancellationToken cancellationToken = default);
}
