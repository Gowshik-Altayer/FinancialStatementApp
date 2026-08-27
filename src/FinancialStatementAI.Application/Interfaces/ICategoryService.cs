using FinancialStatementAI.Application.DTOs.Categories;

namespace FinancialStatementAI.Application.Interfaces;

public interface ICategoryService
{
    /// <summary>The active categories a reviewer can pick from when correcting a transaction's
    /// classification (requirement #6: categories are editable/extensible, not a fixed enum).</summary>
    Task<IReadOnlyList<CategoryResponse>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Every category, active or not — the Categories management page (requirement 10).</summary>
    Task<IReadOnlyList<CategoryDetailResponse>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<CategoryDetailResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Admin-only at the controller level — creating/editing/deactivating the category
    /// taxonomy itself is a different kind of action than a Reviewer correcting one transaction.</summary>
    Task<CategoryMutationResult> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default);

    Task<CategoryMutationResult> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default);

    /// <summary>Soft-delete only (IsActive = false) — never a hard delete, since transactions
    /// reference CategoryId and a category's history must stay inspectable.</summary>
    Task<CategoryMutationResult> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>Per-category transaction count/total spend/AI-vs-human-corrected split, scoped to
    /// the current user's own transactions (requirement 10's category cards + distribution chart).</summary>
    Task<IReadOnlyList<CategoryStatsResponse>> GetStatsAsync(Guid userId, CancellationToken cancellationToken = default);
}
