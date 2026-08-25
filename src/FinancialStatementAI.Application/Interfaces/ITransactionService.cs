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

    /// <summary>Applies a human's category correction to one transaction (requirement #9) and
    /// records it as an audit row.</summary>
    Task<CorrectTransactionResult> CorrectCategoryAsync(Guid transactionId, Guid userId, CorrectTransactionRequest request, CancellationToken cancellationToken = default);
}
