using System.Text.Json;
using FinancialStatementAI.Application.Interfaces;
using StackExchange.Redis;

namespace FinancialStatementAI.Infrastructure.Caching;

/// <summary>Real ICacheService, active when "Caching:Provider" = "Redis" — shared across every
/// instance of a horizontally-scaled deployment, unlike InMemoryCacheService.</summary>
public class RedisCacheService(IConnectionMultiplexer connectionMultiplexer) : ICacheService
{
    public async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        var database = connectionMultiplexer.GetDatabase();

        var cached = await database.StringGetAsync(key);
        if (cached.HasValue)
        {
            var deserialized = JsonSerializer.Deserialize<T>(cached!);
            if (deserialized is not null)
            {
                return deserialized;
            }
        }

        var value = await factory(cancellationToken);
        await database.StringSetAsync(key, JsonSerializer.Serialize(value), expiry);
        return value;
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var database = connectionMultiplexer.GetDatabase();
        await database.KeyDeleteAsync(key);
    }
}
