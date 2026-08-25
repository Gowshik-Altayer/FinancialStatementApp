using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Application.Services;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace FinancialStatementAI.UnitTests;

public class CategoryServiceTests
{
    private readonly Mock<ICategoryRepository> _repository = new();
    private readonly ICacheService _cache = new InMemoryCacheService(new MemoryCache(new MemoryCacheOptions()));

    private CategoryService CreateService() => new(_repository.Object, _cache);

    [Fact]
    public async Task GetActiveAsync_Maps_And_Sorts_Categories_By_Name()
    {
        _repository.Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new Category { Id = Guid.NewGuid(), Name = "Travel" },
            new Category { Id = Guid.NewGuid(), Name = "Groceries" }
        ]);

        var result = await CreateService().GetActiveAsync();

        Assert.Equal(["Groceries", "Travel"], result.Select(c => c.Name));
    }

    [Fact]
    public async Task GetActiveAsync_Only_Hits_The_Repository_Once_Across_Repeated_Calls()
    {
        _repository.Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new Category { Id = Guid.NewGuid(), Name = "Groceries" }
        ]);
        var service = CreateService();

        await service.GetActiveAsync();
        await service.GetActiveAsync();
        await service.GetActiveAsync();

        _repository.Verify(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
