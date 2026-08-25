namespace FinancialStatementAI.Application.Interfaces;

/// <summary>Abstracts read-through caching behind a swappable provider (Phase 15) — the same
/// pattern as every other technology abstraction in this codebase: a zero-configuration default
/// (in-process memory, per-instance only) and a real Redis-backed implementation (shared across
/// every instance of a horizontally-scaled deployment) selected via "Caching:Provider" =
/// "Redis". The business layer never depends on StackExchange.Redis directly.</summary>
public interface ICacheService
{
    /// <summary>Returns the cached value for <paramref name="key"/> if present and unexpired;
    /// otherwise calls <paramref name="factory"/>, caches its result for <paramref name="expiry"/>,
    /// and returns it. Never caches a factory failure — a thrown exception propagates as-is and
    /// nothing is stored, so the next call retries rather than serving a poisoned cache entry.</summary>
    Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan expiry, CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
