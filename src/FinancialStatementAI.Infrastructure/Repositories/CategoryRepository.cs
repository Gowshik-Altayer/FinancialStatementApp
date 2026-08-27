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
        var rows = await dbContext.Transactions
            .Where(t => t.Statement!.UserId == userId && t.CategoryId != null)
            .GroupBy(t => new { t.CategoryId, CategoryName = t.Category!.Name })
            .Select(g => new
            {
                CategoryId = g.Key.CategoryId!.Value,
                g.Key.CategoryName,
                TransactionCount = g.Count(),
                // Magnitude sum, not a signed total — see DashboardService.BuildCategoryBreakdown
                // for why (a mixed debit/credit category would otherwise partially cancel out).
                TotalAmount = g.Sum(t => Math.Abs(t.Amount ?? 0m)),
                CorrectedCount = g.Count(t => t.Corrections.Any())
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => (r.CategoryId, r.CategoryName, r.TransactionCount, r.TotalAmount, r.CorrectedCount)).ToList();
    }
}
