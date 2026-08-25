using FinancialStatementAI.Application.DTOs.Statements;

namespace FinancialStatementAI.Application.Interfaces;

public interface IStatementProcessingService
{
    /// <summary>Runs the full processing pipeline for a statement and returns its updated detail,
    /// or null if it doesn't exist or isn't owned by <paramref name="userId"/>. This method itself
    /// always runs synchronously to completion — it's IBackgroundJobScheduler (Phase 14) that
    /// decides whether it's called directly (the default) or dispatched to a Hangfire worker,
    /// which is why this signature never needed to change to support that.</summary>
    Task<StatementDetailResponse?> ProcessAsync(Guid statementId, Guid userId, CancellationToken cancellationToken = default);
}
