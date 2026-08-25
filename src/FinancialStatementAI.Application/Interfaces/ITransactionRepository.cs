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

    /// <summary>A single transaction with its Statement (for ownership checks), Category,
    /// Classifications, and Corrections all loaded — the shape the review UI needs.</summary>
    Task<Transaction?> GetByIdAsync(Guid transactionId, CancellationToken cancellationToken = default);

    /// <summary>Every transaction belonging to one of the user's statements that is still
    /// awaiting human review (<see cref="Domain.Enums.StatementProcessingStatus.PendingReview"/>),
    /// across all statements — the cross-statement review queue (Phase 12).</summary>
    Task<IReadOnlyList<Transaction>> GetReviewQueueAsync(Guid userId, CancellationToken cancellationToken = default);

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

    /// <summary>Applies a human's category correction (requirement #9): sets the transaction's
    /// live CategoryId to <paramref name="categoryId"/> and persists <paramref name="correction"/>
    /// as an immutable audit row — the original AI-assigned category is never overwritten, only
    /// superseded.</summary>
    Task ApplyCorrectionAsync(Guid transactionId, Guid categoryId, TransactionCorrection correction, CancellationToken cancellationToken = default);
}
