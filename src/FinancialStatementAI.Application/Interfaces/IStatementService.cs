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
}
