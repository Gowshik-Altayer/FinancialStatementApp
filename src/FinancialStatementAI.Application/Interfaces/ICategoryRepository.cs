using FinancialStatementAI.Domain.Entities;

namespace FinancialStatementAI.Application.Interfaces;

public interface ICategoryRepository
{
    Task<IReadOnlyList<Category>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<Category?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Every category, active or not — the Categories management page (requirement 10),
    /// as opposed to GetAllActiveAsync's review-picker-only active set.</summary>
    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Category category, CancellationToken cancellationToken = default);
    Task UpdateAsync(Category category, CancellationToken cancellationToken = default);

    /// <summary>Per-category transaction count/total amount/correction count, scoped to one
    /// user's own transactions — the raw numbers CategoryService turns into
    /// CategoryStatsResponse's percentages.</summary>
    Task<IReadOnlyList<(Guid CategoryId, string CategoryName, int TransactionCount, decimal TotalAmount, int CorrectedCount)>> GetStatsForUserAsync(
        Guid userId, CancellationToken cancellationToken = default);
}
