using FinancialStatementAI.Application.DTOs.Categories;
using FinancialStatementAI.Application.Interfaces;

namespace FinancialStatementAI.Application.Services;

public class CategoryService(ICategoryRepository categoryRepository) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryResponse>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var categories = await categoryRepository.GetAllActiveAsync(cancellationToken);
        return categories
            .OrderBy(c => c.Name)
            .Select(c => new CategoryResponse { Id = c.Id, Name = c.Name })
            .ToList();
    }
}
