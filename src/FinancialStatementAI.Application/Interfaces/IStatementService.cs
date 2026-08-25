using FinancialStatementAI.Application.DTOs.Common;
using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Application.Interfaces;

public interface IStatementService
{
    Task<UploadStatementResult> UploadAsync(
        Guid userId,
        byte[] content,
        string fileName,
        long fileSizeBytes,
        CancellationToken cancellationToken = default);

    /// <summary>Search/filter/paginate the current user's statements (Phase 13). All filters are
    /// optional; page/pageSize are clamped to sane bounds (Domain.Constants.PaginationDefaults).</summary>
    Task<PagedResult<StatementSummaryResponse>> SearchAsync(
        Guid userId,
        string? search,
        StatementProcessingStatus? status,
        ReconciliationStatus? reconciliationStatus,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<StatementDetailResponse?> GetByIdAsync(Guid statementId, Guid userId, CancellationToken cancellationToken = default);

    Task<StatementStatusResponse?> GetStatusAsync(Guid statementId, Guid userId, CancellationToken cancellationToken = default);

    Task<ReconciliationResponse?> GetReconciliationAsync(Guid statementId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Marks a statement Verified — the terminal state after a human has reviewed its
    /// AI-classified transactions and reconciliation result. Only valid from PendingReview.</summary>
    Task<VerifyStatementResult> VerifyAsync(Guid statementId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Requests a reprocess of one statement via the configured IBackgroundJobScheduler
    /// (Phase 14) — synchronous by default, or enqueued for a separate Hangfire worker when
    /// "BackgroundJobs:Provider" = "Hangfire". Returns a snapshot of the statement taken right
    /// after scheduling: the final result for the synchronous default, or the just-set
    /// "Processing" state for the Hangfire path — the caller can tell which happened from
    /// <c>ProcessingStatus</c> alone. Null if the statement doesn't exist or belongs to another
    /// user (no job is scheduled in that case).</summary>
    Task<StatementDetailResponse?> RequestReprocessAsync(Guid statementId, Guid userId, CancellationToken cancellationToken = default);
}
