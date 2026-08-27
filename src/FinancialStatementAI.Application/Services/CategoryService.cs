using FinancialStatementAI.Application.DTOs.Categories;
using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Entities;

namespace FinancialStatementAI.Application.Services;

public class CategoryService(ICategoryRepository categoryRepository, ICacheService cacheService) : ICategoryService
{
    private const string ActiveCategoriesCacheKey = "categories:active";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public Task<IReadOnlyList<CategoryResponse>> GetActiveAsync(CancellationToken cancellationToken = default) =>
        // Read-heavy (the review UI's correction picker fetches this on every page load). Now
        // that category management (create/edit/deactivate) actually exists, every mutation
        // below explicitly invalidates this key — it's no longer the "nothing can change this at
        // runtime" cache it started as.
        cacheService.GetOrCreateAsync(ActiveCategoriesCacheKey, LoadActiveCategoriesAsync, CacheDuration, cancellationToken);

    public async Task<IReadOnlyList<CategoryDetailResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var categories = await categoryRepository.GetAllAsync(cancellationToken);
        return categories.Select(ToDetailResponse).ToList();
    }

    public async Task<CategoryDetailResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await categoryRepository.GetByIdAsync(id, cancellationToken);
        return category is null ? null : ToDetailResponse(category);
    }

    public async Task<CategoryMutationResult> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            return CategoryMutationResult.Failure("Name is required.");
        }

        var existing = await categoryRepository.GetByNameAsync(name, cancellationToken);
        if (existing is not null)
        {
            return CategoryMutationResult.Failure($"A category named \"{name}\" already exists.");
        }

        var category = new Category { Name = name, Description = request.Description, IsSystemDefined = false, IsActive = true };
        await categoryRepository.AddAsync(category, cancellationToken);
        await cacheService.RemoveAsync(ActiveCategoriesCacheKey, cancellationToken);

        return CategoryMutationResult.Success(ToDetailResponse(category));
    }

    public async Task<CategoryMutationResult> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await categoryRepository.GetByIdAsync(id, cancellationToken);
        if (category is null)
        {
            return CategoryMutationResult.AsNotFound();
        }

        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            return CategoryMutationResult.Failure("Name is required.");
        }

        if (!string.Equals(name, category.Name, StringComparison.OrdinalIgnoreCase))
        {
            var existing = await categoryRepository.GetByNameAsync(name, cancellationToken);
            if (existing is not null && existing.Id != id)
            {
                return CategoryMutationResult.Failure($"A category named \"{name}\" already exists.");
            }
        }

        category.Name = name;
        category.Description = request.Description;
        await categoryRepository.UpdateAsync(category, cancellationToken);
        await cacheService.RemoveAsync(ActiveCategoriesCacheKey, cancellationToken);

        return CategoryMutationResult.Success(ToDetailResponse(category));
    }

    public async Task<CategoryMutationResult> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var category = await categoryRepository.GetByIdAsync(id, cancellationToken);
        if (category is null)
        {
            return CategoryMutationResult.AsNotFound();
        }

        category.IsActive = isActive;
        await categoryRepository.UpdateAsync(category, cancellationToken);
        await cacheService.RemoveAsync(ActiveCategoriesCacheKey, cancellationToken);

        return CategoryMutationResult.Success(ToDetailResponse(category));
    }

    public async Task<IReadOnlyList<CategoryStatsResponse>> GetStatsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var stats = await categoryRepository.GetStatsForUserAsync(userId, cancellationToken);

        return stats
            .Select(s =>
            {
                var correctedPercent = s.TransactionCount > 0 ? Math.Round(100m * s.CorrectedCount / s.TransactionCount, 1) : 0m;
                return new CategoryStatsResponse
                {
                    CategoryId = s.CategoryId,
                    CategoryName = s.CategoryName,
                    TransactionCount = s.TransactionCount,
                    TotalAmount = s.TotalAmount,
                    HumanCorrectedPercent = correctedPercent,
                    AiClassifiedPercent = 100m - correctedPercent
                };
            })
            .OrderByDescending(s => s.TransactionCount)
            .ToList();
    }

    private async Task<IReadOnlyList<CategoryResponse>> LoadActiveCategoriesAsync(CancellationToken cancellationToken)
    {
        var categories = await categoryRepository.GetAllActiveAsync(cancellationToken);
        return categories
            .OrderBy(c => c.Name)
            .Select(c => new CategoryResponse { Id = c.Id, Name = c.Name })
            .ToList();
    }

    private static CategoryDetailResponse ToDetailResponse(Category category) => new()
    {
        Id = category.Id,
        Name = category.Name,
        Description = category.Description,
        IsSystemDefined = category.IsSystemDefined,
        IsActive = category.IsActive,
        CreatedAt = category.CreatedAt
    };
}
