using FinancialStatementAI.Application.DTOs.Common;
using FinancialStatementAI.Application.DTOs.Transactions;
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

    /// <summary>The bulk counterpart to <see cref="ApplyCorrectionAsync"/>: applies the same
    /// category correction to every transaction the user owns that shares the given exact
    /// Merchant text, each getting its own audit-trail TransactionCorrection row so a bulk
    /// correction is indistinguishable from a solo one in the history afterward. A transaction
    /// already on the target category is left untouched (and not counted) — re-asserting the
    /// same value isn't a real edit.</summary>
    /// <returns>How many transactions were actually changed.</returns>
    Task<int> ApplyBulkCorrectionByMerchantAsync(
        Guid userId,
        string merchant,
        Guid categoryId,
        string categoryName,
        string? reason,
        Guid correctedByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Search/filter/paginate across every transaction belonging to one of the user's
    /// statements (Phase 13) — the "All Transactions" page, as opposed to the single-statement
    /// list or the PendingReview-only review queue. Runs the filter/count/page entirely in the
    /// database (selecting just matching Ids) before hydrating the bounded page of full entities
    /// with their Category/Classifications/Corrections.</summary>
    Task<PagedResult<Transaction>> SearchAsync(Guid userId, TransactionSearchFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Unfiltered counts for the Transactions page's KPI row — see
    /// ITransactionService.GetSummaryAsync for why this never takes a filter.</summary>
    Task<TransactionSummaryResponse> GetSummaryAsync(Guid userId, CancellationToken cancellationToken = default);
}
