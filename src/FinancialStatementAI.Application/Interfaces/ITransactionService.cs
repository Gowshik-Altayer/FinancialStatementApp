using FinancialStatementAI.Application.DTOs.Common;
using FinancialStatementAI.Application.DTOs.Transactions;

namespace FinancialStatementAI.Application.Interfaces;

public interface ITransactionService
{
    /// <summary>All transactions for one statement, in date order — null if the statement
    /// doesn't exist or belongs to another user.</summary>
    Task<IReadOnlyList<TransactionResponse>?> GetForStatementAsync(Guid statementId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The cross-statement human review queue (Phase 12): every transaction on one of
    /// this user's PendingReview statements, lowest classification confidence first.</summary>
    Task<IReadOnlyList<TransactionResponse>> GetReviewQueueAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Search/filter/paginate across all of the user's transactions, across every
    /// statement regardless of processing status (Phase 13's "All Transactions" page — as
    /// opposed to the single-statement list or the PendingReview-only review queue).</summary>
    Task<PagedResult<TransactionResponse>> SearchAsync(Guid userId, TransactionSearchFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Unfiltered KPI counts for the Transactions page's summary row — always reflects
    /// the user's full transaction set, not whatever filter is currently applied, so the KPIs
    /// read as stable totals rather than shifting with every search keystroke.</summary>
    Task<TransactionSummaryResponse> GetSummaryAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Applies a human's category correction to one transaction (requirement #9) and
    /// records it as an audit row.</summary>
    Task<CorrectTransactionResult> CorrectCategoryAsync(Guid transactionId, Guid userId, CorrectTransactionRequest request, CancellationToken cancellationToken = default);
}
