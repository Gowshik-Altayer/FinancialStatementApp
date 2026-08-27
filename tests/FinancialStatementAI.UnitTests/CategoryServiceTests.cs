using FinancialStatementAI.Application.DTOs.Categories;
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

    [Fact]
    public async Task CreateAsync_Rejects_Blank_Name()
    {
        var result = await CreateService().CreateAsync(new CreateCategoryRequest { Name = "   " });

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Error);
        _repository.Verify(r => r.AddAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_Rejects_Duplicate_Name()
    {
        _repository.Setup(r => r.GetByNameAsync("Travel", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Category { Id = Guid.NewGuid(), Name = "Travel" });

        var result = await CreateService().CreateAsync(new CreateCategoryRequest { Name = "Travel" });

        Assert.False(result.Succeeded);
        _repository.Verify(r => r.AddAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_Adds_Category_And_Invalidates_Active_Cache()
    {
        _repository.Setup(r => r.GetByNameAsync("Travel", It.IsAny<CancellationToken>())).ReturnsAsync((Category?)null);
        _repository.Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new Category { Id = Guid.NewGuid(), Name = "Groceries" }
        ]);
        var service = CreateService();
        await service.GetActiveAsync();

        var result = await service.CreateAsync(new CreateCategoryRequest { Name = "Travel", Description = "Trips" });

        Assert.True(result.Succeeded);
        Assert.Equal("Travel", result.Category!.Name);
        _repository.Verify(r => r.AddAsync(It.Is<Category>(c => c.Name == "Travel" && !c.IsSystemDefined && c.IsActive), It.IsAny<CancellationToken>()), Times.Once);

        _repository.Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new Category { Id = Guid.NewGuid(), Name = "Groceries" },
            new Category { Id = Guid.NewGuid(), Name = "Travel" }
        ]);
        await service.GetActiveAsync();
        _repository.Verify(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task UpdateAsync_Returns_NotFound_For_Unknown_Id()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Category?)null);

        var result = await CreateService().UpdateAsync(Guid.NewGuid(), new UpdateCategoryRequest { Name = "Travel" });

        Assert.True(result.NotFound);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task UpdateAsync_Rejects_Rename_To_Another_Categorys_Name()
    {
        var id = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        _repository.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new Category { Id = id, Name = "Groceries" });
        _repository.Setup(r => r.GetByNameAsync("Travel", It.IsAny<CancellationToken>())).ReturnsAsync(new Category { Id = otherId, Name = "Travel" });

        var result = await CreateService().UpdateAsync(id, new UpdateCategoryRequest { Name = "Travel" });

        Assert.False(result.Succeeded);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_Allows_Keeping_The_Same_Name()
    {
        var id = Guid.NewGuid();
        _repository.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new Category { Id = id, Name = "Groceries" });

        var result = await CreateService().UpdateAsync(id, new UpdateCategoryRequest { Name = "Groceries", Description = "Food" });

        Assert.True(result.Succeeded);
        _repository.Verify(r => r.GetByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.UpdateAsync(It.Is<Category>(c => c.Description == "Food"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetActiveAsync_Returns_NotFound_For_Unknown_Id()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Category?)null);

        var result = await CreateService().SetActiveAsync(Guid.NewGuid(), false);

        Assert.True(result.NotFound);
    }

    [Fact]
    public async Task SetActiveAsync_Toggles_IsActive_And_Persists()
    {
        var id = Guid.NewGuid();
        var category = new Category { Id = id, Name = "Travel", IsActive = true };
        _repository.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(category);

        var result = await CreateService().SetActiveAsync(id, false);

        Assert.True(result.Succeeded);
        Assert.False(result.Category!.IsActive);
        _repository.Verify(r => r.UpdateAsync(It.Is<Category>(c => !c.IsActive), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetStatsAsync_Computes_HumanCorrected_And_AiClassified_Percentages()
    {
        var categoryId = Guid.NewGuid();
        _repository.Setup(r => r.GetStatsForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            (categoryId, "Groceries", TransactionCount: 4, TotalAmount: 200m, CorrectedCount: 1)
        ]);

        var result = await CreateService().GetStatsAsync(Guid.NewGuid());

        var stats = Assert.Single(result);
        Assert.Equal(25m, stats.HumanCorrectedPercent);
        Assert.Equal(75m, stats.AiClassifiedPercent);
    }

    [Fact]
    public async Task GetStatsAsync_Orders_By_TransactionCount_Descending()
    {
        _repository.Setup(r => r.GetStatsForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            (Guid.NewGuid(), "Travel", TransactionCount: 2, TotalAmount: 50m, CorrectedCount: 0),
            (Guid.NewGuid(), "Groceries", TransactionCount: 9, TotalAmount: 400m, CorrectedCount: 0)
        ]);

        var result = await CreateService().GetStatsAsync(Guid.NewGuid());

        Assert.Equal(["Groceries", "Travel"], result.Select(s => s.CategoryName));
    }

    [Fact]
    public async Task GetByIdAsync_Returns_Null_For_Unknown_Id()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Category?)null);

        var result = await CreateService().GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_Returns_Both_Active_And_Inactive_Categories()
    {
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new Category { Id = Guid.NewGuid(), Name = "Travel", IsActive = true },
            new Category { Id = Guid.NewGuid(), Name = "Archived", IsActive = false }
        ]);

        var result = await CreateService().GetAllAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, c => c is { Name: "Archived", IsActive: false });
    }
}
