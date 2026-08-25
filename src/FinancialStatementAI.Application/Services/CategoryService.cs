using FinancialStatementAI.Application.DTOs.Categories;
using FinancialStatementAI.Application.Interfaces;

namespace FinancialStatementAI.Application.Services;

public class CategoryService(ICategoryRepository categoryRepository, ICacheService cacheService) : ICategoryService
{
    private const string ActiveCategoriesCacheKey = "categories:active";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public Task<IReadOnlyList<CategoryResponse>> GetActiveAsync(CancellationToken cancellationToken = default) =>
        // Read-heavy (the review UI's correction picker fetches this on every page load),
        // write-rare (category management is a later phase — nothing invalidates this cache yet
        // because nothing can currently change the active category list at runtime), so a plain
        // time-based cache is enough (Phase 15) — no invalidation logic to get wrong today.
        cacheService.GetOrCreateAsync(ActiveCategoriesCacheKey, LoadActiveCategoriesAsync, CacheDuration, cancellationToken);

    private async Task<IReadOnlyList<CategoryResponse>> LoadActiveCategoriesAsync(CancellationToken cancellationToken)
    {
        var categories = await categoryRepository.GetAllActiveAsync(cancellationToken);
        return categories
            .OrderBy(c => c.Name)
            .Select(c => new CategoryResponse { Id = c.Id, Name = c.Name })
            .ToList();
    }
}
