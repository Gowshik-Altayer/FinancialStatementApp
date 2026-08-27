using FinancialStatementAI.Application.DTOs.Common;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Application.Interfaces;

public interface IReconciliationRepository
{
    /// <summary>Appends a new reconciliation run — never overwrites a prior one, so a
    /// statement's reconciliation history stays inspectable across reprocesses.</summary>
    Task AddAsync(ReconciliationResult result, CancellationToken cancellationToken = default);

    Task<ReconciliationResult?> GetLatestAsync(Guid statementId, CancellationToken cancellationToken = default);

    /// <summary>The current (most recent) reconciliation result for every one of the user's
    /// statements that has at least one — the cross-statement Reconciliation page (requirement 9).
    /// Statements never yet reconciled are excluded here (they show up in
    /// GetSummaryCountsAsync's PendingCount instead, not as a row with no data to show).</summary>
    Task<PagedResult<(ReconciliationResult Result, Statement Statement)>> GetCurrentForUserAsync(
        Guid userId, ReconciliationStatus? status, string? search, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Aggregate counts across every one of the user's statements' current reconciliation
    /// results, for the Reconciliation page's KPI row and status chart.</summary>
    Task<(int Reconciled, int Mismatch, int InsufficientInformation, int Pending, decimal TotalMismatchDiscrepancy)> GetSummaryCountsAsync(
        Guid userId, CancellationToken cancellationToken = default);
}
