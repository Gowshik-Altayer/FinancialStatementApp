using FinancialStatementAI.Application.Interfaces;
using StackExchange.Redis;

namespace FinancialStatementAI.Infrastructure.Caching;

/// <summary>Real IDistributedLockService, active when "Caching:Provider" = "Redis" — guards
/// across every instance of a horizontally-scaled deployment, unlike
/// InMemoryDistributedLockService. Uses the standard single-instance Redis lock recipe: acquire
/// via <c>SET key token NX PX expiry</c> (atomic — only succeeds if the key doesn't already
/// exist), release via a Lua script that only deletes the key if it still holds *this* handle's
/// own token, so a handle can never release a lock it no longer owns (e.g. one that already
/// expired and was re-acquired by someone else in the meantime).</summary>
public class RedisDistributedLockService(IConnectionMultiplexer connectionMultiplexer) : IDistributedLockService
{
    private const string ReleaseScript = """
        if redis.call("get", KEYS[1]) == ARGV[1] then
            return redis.call("del", KEYS[1])
        else
            return 0
        end
        """;

    public async Task<IAsyncDisposable?> TryAcquireAsync(string resource, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        var database = connectionMultiplexer.GetDatabase();
        var token = Guid.NewGuid().ToString("N");

        var acquired = await database.StringSetAsync(resource, token, expiry, When.NotExists);
        return acquired ? new LockHandle(database, resource, token) : null;
    }

    private sealed class LockHandle(IDatabase database, string resource, string token) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await database.ScriptEvaluateAsync(ReleaseScript, [(RedisKey)resource], [(RedisValue)token]);
        }
    }
}
