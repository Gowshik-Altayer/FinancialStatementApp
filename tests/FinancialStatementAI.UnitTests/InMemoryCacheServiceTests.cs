using FinancialStatementAI.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace FinancialStatementAI.UnitTests;

public class InMemoryCacheServiceTests
{
    private static InMemoryCacheService CreateService() => new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task GetOrCreateAsync_Calls_The_Factory_Only_Once_For_Repeated_Reads()
    {
        var service = CreateService();
        var callCount = 0;

        async Task<int> Factory(CancellationToken _)
        {
            callCount++;
            return await Task.FromResult(42);
        }

        var first = await service.GetOrCreateAsync("key", Factory, TimeSpan.FromMinutes(1));
        var second = await service.GetOrCreateAsync("key", Factory, TimeSpan.FromMinutes(1));

        Assert.Equal(42, first);
        Assert.Equal(42, second);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task Different_Keys_Are_Cached_Independently()
    {
        var service = CreateService();

        var a = await service.GetOrCreateAsync("a", _ => Task.FromResult("value-a"), TimeSpan.FromMinutes(1));
        var b = await service.GetOrCreateAsync("b", _ => Task.FromResult("value-b"), TimeSpan.FromMinutes(1));

        Assert.Equal("value-a", a);
        Assert.Equal("value-b", b);
    }

    [Fact]
    public async Task RemoveAsync_Forces_The_Next_Read_To_Call_The_Factory_Again()
    {
        var service = CreateService();
        var callCount = 0;

        Task<int> Factory(CancellationToken _)
        {
            callCount++;
            return Task.FromResult(callCount);
        }

        var first = await service.GetOrCreateAsync("key", Factory, TimeSpan.FromMinutes(1));
        await service.RemoveAsync("key");
        var second = await service.GetOrCreateAsync("key", Factory, TimeSpan.FromMinutes(1));

        Assert.Equal(1, first);
        Assert.Equal(2, second);
    }

    [Fact]
    public async Task A_Factory_Exception_Is_Never_Cached_So_The_Next_Call_Retries()
    {
        var service = CreateService();
        var attempt = 0;

        Task<int> Factory(CancellationToken _)
        {
            attempt++;
            return attempt == 1 ? Task.FromException<int>(new InvalidOperationException("transient")) : Task.FromResult(99);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetOrCreateAsync("key", Factory, TimeSpan.FromMinutes(1)));
        var result = await service.GetOrCreateAsync("key", Factory, TimeSpan.FromMinutes(1));

        Assert.Equal(99, result);
        Assert.Equal(2, attempt);
    }
}
