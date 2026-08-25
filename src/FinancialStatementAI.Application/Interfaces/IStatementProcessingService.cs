using FinancialStatementAI.Application.DTOs.Statements;

namespace FinancialStatementAI.Application.Interfaces;

public interface IStatementProcessingService
{
    /// <summary>Runs the extraction step(s) currently available for a statement and returns its
    /// updated detail, or null if it doesn't exist or isn't owned by <paramref name="userId"/>.
    /// Runs synchronously for now (called directly from the reprocess endpoint) — Phase 14 moves
    /// the trigger to a Hangfire background job without changing this contract.</summary>
    Task<StatementDetailResponse?> ProcessAsync(Guid statementId, Guid userId, CancellationToken cancellationToken = default);
}
