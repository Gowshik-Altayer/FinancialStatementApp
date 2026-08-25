using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Application.Services;
using FinancialStatementAI.Domain.Entities;
using Moq;

namespace FinancialStatementAI.UnitTests;

public class CategoryServiceTests
{
    [Fact]
    public async Task GetActiveAsync_Maps_And_Sorts_Categories_By_Name()
    {
        var repository = new Mock<ICategoryRepository>();
        repository.Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new Category { Id = Guid.NewGuid(), Name = "Travel" },
            new Category { Id = Guid.NewGuid(), Name = "Groceries" }
        ]);

        var result = await new CategoryService(repository.Object).GetActiveAsync();

        Assert.Equal(["Groceries", "Travel"], result.Select(c => c.Name));
    }
}
