using FinancialStatementAI.Application.DTOs.Categories;

namespace FinancialStatementAI.Application.Interfaces;

public interface ICategoryService
{
    /// <summary>The active categories a reviewer can pick from when correcting a transaction's
    /// classification (requirement #6: categories are editable/extensible, not a fixed enum).</summary>
    Task<IReadOnlyList<CategoryResponse>> GetActiveAsync(CancellationToken cancellationToken = default);
}
