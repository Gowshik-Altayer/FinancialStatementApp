using FinancialStatementAI.Infrastructure.Caching;

namespace FinancialStatementAI.UnitTests;

public class InMemoryDistributedLockServiceTests
{
    [Fact]
    public async Task A_Second_Acquire_On_The_Same_Resource_Fails_While_The_First_Is_Held()
    {
        var service = new InMemoryDistributedLockService();

        var first = await service.TryAcquireAsync("resource", TimeSpan.FromMinutes(1));
        var second = await service.TryAcquireAsync("resource", TimeSpan.FromMinutes(1));

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public async Task Disposing_The_Handle_Releases_The_Lock_For_The_Next_Acquirer()
    {
        var service = new InMemoryDistributedLockService();

        var first = await service.TryAcquireAsync("resource", TimeSpan.FromMinutes(1));
        Assert.NotNull(first);
        await first!.DisposeAsync();

        var second = await service.TryAcquireAsync("resource", TimeSpan.FromMinutes(1));

        Assert.NotNull(second);
    }

    [Fact]
    public async Task Different_Resources_Can_Be_Locked_Independently()
    {
        var service = new InMemoryDistributedLockService();

        var a = await service.TryAcquireAsync("resource-a", TimeSpan.FromMinutes(1));
        var b = await service.TryAcquireAsync("resource-b", TimeSpan.FromMinutes(1));

        Assert.NotNull(a);
        Assert.NotNull(b);
    }
}
