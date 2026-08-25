using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Application.Interfaces;

public interface ITransactionRepository
{
    /// <summary>Replaces all transactions currently attached to a statement with a freshly
    /// parsed set (so reprocessing doesn't accumulate duplicates of the statement's own prior
    /// parse), flagging cross-statement duplicates (requirement #21 — never auto-deleted, only
    /// flagged for review) against the same user's other transactions in the same pass.</summary>
    Task ReplaceForStatementAsync(Guid statementId, Guid userId, IEnumerable<Transaction> transactions, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Transaction>> GetByStatementIdAsync(Guid statementId, CancellationToken cancellationToken = default);

    /// <summary>Records a new classification attempt (never overwriting a prior one — requirement
    /// #9) and, if it's the most confident/current attempt, updates the transaction's live
    /// CategoryId to match.</summary>
    Task ApplyClassificationAsync(
        Guid transactionId,
        Guid categoryId,
        decimal confidenceScore,
        ClassificationMethod method,
        string? reason,
        CancellationToken cancellationToken = default);
}
