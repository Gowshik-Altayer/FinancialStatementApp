using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinancialStatementAI.Infrastructure.Repositories;

public class CategoryRepository(AppDbContext dbContext) : ICategoryRepository
{
    public async Task<IReadOnlyList<Category>> GetAllActiveAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Categories.Where(c => c.IsActive).AsNoTracking().ToListAsync(cancellationToken);

    public Task<Category?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
        dbContext.Categories.AsNoTracking().SingleOrDefaultAsync(c => c.Name == name, cancellationToken);

    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Categories.OrderBy(c => c.Name).AsNoTracking().ToListAsync(cancellationToken);

    public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task AddAsync(Category category, CancellationToken cancellationToken = default)
    {
        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Category category, CancellationToken cancellationToken = default)
    {
        dbContext.Categories.Update(category);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<(Guid CategoryId, string CategoryName, int TransactionCount, decimal TotalAmount, int CorrectedCount)>> GetStatsForUserAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var baseStats = await dbContext.Transactions
            .Where(t => t.Statement!.UserId == userId && t.CategoryId != null)
            .GroupBy(t => new { t.CategoryId, CategoryName = t.Category!.Name })
            .Select(g => new
            {
                CategoryId = g.Key.CategoryId!.Value,
                g.Key.CategoryName,
                TransactionCount = g.Count(),
                // Magnitude sum, not a signed total — see DashboardService.BuildCategoryBreakdown
                // for why (a mixed debit/credit category would otherwise partially cancel out).
                TotalAmount = g.Sum(t => Math.Abs(t.Amount ?? 0m))
            })
            .ToListAsync(cancellationToken);

        // Queried separately from the stats above: g.Count(t => t.Corrections.Any()) nests a
        // correlated subquery inside an aggregate, which SQL Server rejects ("Cannot perform an
        // aggregate function on an expression containing an aggregate or a subquery") — this
        // survives even projecting the Any() into a bool column first, since EF still flattens
        // it into one query. A separate GroupBy over the already-filtered "has a correction"
        // rows sidesteps the issue entirely.
        var correctedCounts = await dbContext.Transactions
            .Where(t => t.Statement!.UserId == userId && t.CategoryId != null && t.Corrections.Any())
            .GroupBy(t => t.CategoryId)
            .Select(g => new { CategoryId = g.Key!.Value, Count = g.Count() })
            .ToDictionaryAsync(g => g.CategoryId, g => g.Count, cancellationToken);

        return baseStats
            .Select(s => (s.CategoryId, s.CategoryName, s.TransactionCount, s.TotalAmount, correctedCounts.GetValueOrDefault(s.CategoryId)))
            .ToList();
    }
}
