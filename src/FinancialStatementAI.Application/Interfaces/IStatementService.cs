using FinancialStatementAI.Application.DTOs.Statements;

namespace FinancialStatementAI.Application.Interfaces;

public interface IStatementService
{
    Task<UploadStatementResult> UploadAsync(
        Guid userId,
        byte[] content,
        string fileName,
        long fileSizeBytes,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StatementSummaryResponse>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<StatementDetailResponse?> GetByIdAsync(Guid statementId, Guid userId, CancellationToken cancellationToken = default);

    Task<StatementStatusResponse?> GetStatusAsync(Guid statementId, Guid userId, CancellationToken cancellationToken = default);

    Task<ReconciliationResponse?> GetReconciliationAsync(Guid statementId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Marks a statement Verified — the terminal state after a human has reviewed its
    /// AI-classified transactions and reconciliation result. Only valid from PendingReview.</summary>
    Task<VerifyStatementResult> VerifyAsync(Guid statementId, Guid userId, CancellationToken cancellationToken = default);
}
