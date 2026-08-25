using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinancialStatementAI.Infrastructure.Repositories;

public class ReconciliationRepository(AppDbContext dbContext) : IReconciliationRepository
{
    public async Task AddAsync(ReconciliationResult result, CancellationToken cancellationToken = default)
    {
        dbContext.ReconciliationResults.Add(result);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<ReconciliationResult?> GetLatestAsync(Guid statementId, CancellationToken cancellationToken = default) =>
        dbContext.ReconciliationResults
            .Where(r => r.StatementId == statementId)
            .OrderByDescending(r => r.CreatedAt)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
}
