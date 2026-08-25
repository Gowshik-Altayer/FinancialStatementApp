using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Enums;
using FinancialStatementAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinancialStatementAI.Infrastructure.Repositories;

public class ClassificationHistoryRepository(AppDbContext dbContext) : IClassificationHistoryRepository
{
    public async Task<string?> FindPreviousCorrectedCategoryAsync(Guid userId, string merchant, CancellationToken cancellationToken = default)
    {
        var mostRecentCorrection = await dbContext.TransactionCorrections
            .Where(c => c.FieldName == CorrectedField.Category
                && c.Transaction!.Statement!.UserId == userId
                && c.Transaction.Merchant != null
                && c.Transaction.Merchant.ToLower() == merchant.ToLower())
            .OrderByDescending(c => c.CorrectedAt)
            .Select(c => c.CorrectedValue)
            .FirstOrDefaultAsync(cancellationToken);

        return mostRecentCorrection;
    }
}
