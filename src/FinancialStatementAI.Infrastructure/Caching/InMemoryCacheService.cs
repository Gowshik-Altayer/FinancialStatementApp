using FinancialStatementAI.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace FinancialStatementAI.Infrastructure.Caching;

/// <summary>Default ICacheService — process-local only (not shared across instances of a
/// horizontally-scaled deployment, unlike RedisCacheService), but zero configuration required.</summary>
public class InMemoryCacheService(IMemoryCache memoryCache) : ICacheService
{
    public async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        if (memoryCache.TryGetValue(key, out T? cached) && cached is not null)
        {
            return cached;
        }

        var value = await factory(cancellationToken);
        memoryCache.Set(key, value, expiry);
        return value;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        memoryCache.Remove(key);
        return Task.CompletedTask;
    }
}
