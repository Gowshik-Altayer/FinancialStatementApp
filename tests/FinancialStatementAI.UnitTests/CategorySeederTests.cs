using FinancialStatementAI.Domain.Constants;
using FinancialStatementAI.Infrastructure.Persistence;
using FinancialStatementAI.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;

namespace FinancialStatementAI.UnitTests;

public class CategorySeederTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task SeedAsync_Creates_All_Default_Categories_Exactly_Once()
    {
        await using var dbContext = CreateContext();

        await CategorySeeder.SeedAsync(dbContext);

        var names = await dbContext.Categories.Select(c => c.Name).ToListAsync();
        Assert.Equal(DefaultCategories.Names.Count, names.Count);
        Assert.All(DefaultCategories.Names, expected => Assert.Contains(expected, names));
        Assert.All(dbContext.Categories, c => Assert.True(c.IsSystemDefined));
    }

    [Fact]
    public async Task SeedAsync_Is_Idempotent()
    {
        await using var dbContext = CreateContext();

        await CategorySeeder.SeedAsync(dbContext);
        await CategorySeeder.SeedAsync(dbContext);

        var count = await dbContext.Categories.CountAsync();
        Assert.Equal(DefaultCategories.Names.Count, count);
    }
}
