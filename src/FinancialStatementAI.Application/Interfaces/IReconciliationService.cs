using FinancialStatementAI.Application.DTOs.Common;
using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Application.Interfaces;

/// <summary>Deterministic financial reconciliation — requirement #20 is explicit that this must
/// be plain C# arithmetic, never AI: Opening Balance + Credits - Debits = Expected Closing
/// Balance, compared against the statement's own reported closing balance.</summary>
public interface IReconciliationService
{
    Task<ReconciliationResponse> ReconcileAsync(Guid statementId, CancellationToken cancellationToken = default);

    /// <summary>Cross-statement reconciliation list (requirement 9) — the current result for each
    /// of the user's statements that has been reconciled at least once.</summary>
    Task<PagedResult<ReconciliationSummaryResponse>> GetSummaryForUserAsync(
        Guid userId, ReconciliationStatus? status, string? search, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<ReconciliationSummaryCountsResponse> GetSummaryCountsAsync(Guid userId, CancellationToken cancellationToken = default);
}
