using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinancialStatementAI.Infrastructure.Repositories;

public class DashboardRepository(AppDbContext dbContext) : IDashboardRepository
{
    public async Task<IReadOnlyList<Statement>> GetStatementsForDashboardAsync(Guid? userId, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Statements
            .Include(s => s.StatementExtraction)
            .Include(s => s.ReconciliationResults)
            .Include(s => s.Transactions).ThenInclude(t => t.Category)
            .Include(s => s.Transactions).ThenInclude(t => t.Classifications)
            .Include(s => s.Transactions).ThenInclude(t => t.Corrections)
            .AsNoTracking()
            .AsSplitQuery(); // several one-to-many Includes — split avoids a cartesian-product join

        if (userId is not null)
        {
            query = query.Where(s => s.UserId == userId);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TransactionCorrection>> GetRecentCorrectionsAsync(Guid? userId, int take, CancellationToken cancellationToken = default)
    {
        var query = dbContext.TransactionCorrections
            .Include(c => c.Transaction).ThenInclude(t => t!.Statement)
            .AsNoTracking();

        if (userId is not null)
        {
            query = query.Where(c => c.Transaction!.Statement!.UserId == userId);
        }

        return await query
            .OrderByDescending(c => c.CorrectedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }
}
