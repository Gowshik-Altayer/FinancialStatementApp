using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Domain.Enums;
using FinancialStatementAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinancialStatementAI.Infrastructure.Repositories;

public class TransactionRepository(AppDbContext dbContext) : ITransactionRepository
{
    public async Task ReplaceForStatementAsync(
        Guid statementId,
        Guid userId,
        IEnumerable<Transaction> transactions,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.Transactions
            .Where(t => t.StatementId == statementId)
            .ToListAsync(cancellationToken);
        dbContext.Transactions.RemoveRange(existing);

        // Duplicate detection (requirement #21) against the same user's OTHER statements —
        // never this statement's own transactions, since re-running this statement's own parse
        // (reprocess) must not flag a statement as a duplicate of itself.
        var otherStatementIds = await dbContext.Statements
            .Where(s => s.UserId == userId && s.Id != statementId)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var candidates = otherStatementIds.Count == 0
            ? []
            : await dbContext.Transactions
                .Where(t => otherStatementIds.Contains(t.StatementId))
                .Select(t => new { t.Id, t.TransactionDate, t.Amount, t.Merchant })
                .ToListAsync(cancellationToken);

        var newTransactions = transactions.ToList();
        foreach (var transaction in newTransactions)
        {
            var duplicate = candidates.FirstOrDefault(c =>
                c.TransactionDate == transaction.TransactionDate &&
                c.Amount == transaction.Amount &&
                string.Equals(c.Merchant, transaction.Merchant, StringComparison.OrdinalIgnoreCase));

            if (duplicate is not null)
            {
                transaction.IsPotentialDuplicate = true;
                transaction.DuplicateOfTransactionId = duplicate.Id;
            }
        }

        dbContext.Transactions.AddRange(newTransactions);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Transaction>> GetByStatementIdAsync(Guid statementId, CancellationToken cancellationToken = default) =>
        await dbContext.Transactions
            .Where(t => t.StatementId == statementId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task ApplyClassificationAsync(
        Guid transactionId,
        Guid categoryId,
        decimal confidenceScore,
        ClassificationMethod method,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var previousCurrent = await dbContext.TransactionClassifications
            .Where(c => c.TransactionId == transactionId && c.IsCurrent)
            .ToListAsync(cancellationToken);
        foreach (var previous in previousCurrent)
        {
            previous.IsCurrent = false;
        }

        dbContext.TransactionClassifications.Add(new TransactionClassification
        {
            TransactionId = transactionId,
            CategoryId = categoryId,
            ConfidenceScore = confidenceScore,
            ClassificationMethod = method,
            Reason = reason,
            IsCurrent = true
        });

        var transaction = await dbContext.Transactions.SingleAsync(t => t.Id == transactionId, cancellationToken);
        transaction.CategoryId = categoryId;

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
