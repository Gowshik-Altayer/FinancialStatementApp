using FinancialStatementAI.Domain.Entities;

namespace FinancialStatementAI.Application.Interfaces;

public interface ITransactionRepository
{
    /// <summary>Replaces all transactions currently attached to a statement with a freshly
    /// parsed set (so reprocessing doesn't accumulate duplicates of the statement's own prior
    /// parse), flagging cross-statement duplicates (requirement #21 — never auto-deleted, only
    /// flagged for review) against the same user's other transactions in the same pass.</summary>
    Task ReplaceForStatementAsync(Guid statementId, Guid userId, IEnumerable<Transaction> transactions, CancellationToken cancellationToken = default);
}
