using FinancialStatementAI.Application.DTOs.Statements;

namespace FinancialStatementAI.Application.Interfaces;

/// <summary>Deterministic financial reconciliation — requirement #20 is explicit that this must
/// be plain C# arithmetic, never AI: Opening Balance + Credits - Debits = Expected Closing
/// Balance, compared against the statement's own reported closing balance.</summary>
public interface IReconciliationService
{
    Task<ReconciliationResponse> ReconcileAsync(Guid statementId, CancellationToken cancellationToken = default);
}
