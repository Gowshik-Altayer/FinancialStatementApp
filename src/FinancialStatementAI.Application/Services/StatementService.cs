using FinancialStatementAI.Application.DTOs.Common;
using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Constants;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Application.Services;

public class StatementService(
    IStatementFileValidator fileValidator,
    IFileStorageService fileStorage,
    IStatementRepository statementRepository,
    IProcessingJobRepository processingJobRepository,
    IReconciliationRepository reconciliationRepository,
    IBackgroundJobScheduler backgroundJobScheduler) : IStatementService
{
    public async Task<UploadStatementResult> UploadAsync(
        Guid userId,
        byte[] content,
        string fileName,
        long fileSizeBytes,
        CancellationToken cancellationToken = default)
    {
        var validation = fileValidator.Validate(content, fileName, fileSizeBytes);
        if (!validation.IsValid)
        {
            return UploadStatementResult.Failure(validation.ErrorMessage!);
        }

        var storedFilePath = await fileStorage.SaveAsync(new MemoryStream(content), fileName, cancellationToken);

        var statement = new Statement
        {
            UserId = userId,
            OriginalFileName = fileName,
            StoredFilePath = storedFilePath,
            ContentType = validation.ConfirmedContentType,
            FileSizeBytes = fileSizeBytes,
            DocumentType = DocumentType.Unknown,
            ProcessingStatus = StatementProcessingStatus.Uploaded,
            UploadedAt = DateTime.UtcNow
        };
        await statementRepository.AddAsync(statement, cancellationToken);

        await processingJobRepository.AddAsync(new ProcessingJob
        {
            StatementId = statement.Id,
            Stage = ProcessingStage.Upload,
            Status = ProcessingJobStatus.Pending
        }, cancellationToken);

        return UploadStatementResult.Success(StatementMapper.ToDetailResponse(statement));
    }

    public Task<PagedResult<StatementSummaryResponse>> SearchAsync(
        Guid userId,
        string? search,
        StatementProcessingStatus? status,
        ReconciliationStatus? reconciliationStatus,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, PaginationDefaults.MaxPageSize);

        return statementRepository.SearchForUserAsync(userId, search, status, reconciliationStatus, page, pageSize, cancellationToken);
    }

    public async Task<StatementDetailResponse?> GetByIdAsync(Guid statementId, Guid userId, CancellationToken cancellationToken = default)
    {
        var statement = await statementRepository.GetByIdAsync(statementId, cancellationToken);
        return statement is null || statement.UserId != userId ? null : StatementMapper.ToDetailResponse(statement);
    }

    public async Task<StatementStatusResponse?> GetStatusAsync(Guid statementId, Guid userId, CancellationToken cancellationToken = default)
    {
        var statement = await statementRepository.GetByIdAsync(statementId, cancellationToken);
        if (statement is null || statement.UserId != userId)
        {
            return null;
        }

        return new StatementStatusResponse
        {
            Id = statement.Id,
            ProcessingStatus = statement.ProcessingStatus.ToString(),
            UploadedAt = statement.UploadedAt,
            ProcessedAt = statement.ProcessedAt
        };
    }

    public async Task<ReconciliationResponse?> GetReconciliationAsync(Guid statementId, Guid userId, CancellationToken cancellationToken = default)
    {
        var statement = await statementRepository.GetByIdAsync(statementId, cancellationToken);
        if (statement is null || statement.UserId != userId)
        {
            return null;
        }

        var latest = await reconciliationRepository.GetLatestAsync(statementId, cancellationToken);
        return latest is null ? null : ReconciliationService.ToResponse(latest);
    }

    public async Task<VerifyStatementResult> VerifyAsync(Guid statementId, Guid userId, CancellationToken cancellationToken = default)
    {
        var statement = await statementRepository.GetByIdAsync(statementId, cancellationToken);
        if (statement is null || statement.UserId != userId)
        {
            return VerifyStatementResult.AsNotFound();
        }

        if (statement.ProcessingStatus != StatementProcessingStatus.PendingReview)
        {
            return VerifyStatementResult.Failure(
                $"Statement must be in PendingReview to verify (current status: {statement.ProcessingStatus}).");
        }

        await statementRepository.UpdateStatusAsync(statementId, StatementProcessingStatus.Verified, DateTime.UtcNow, cancellationToken);

        var updated = await statementRepository.GetByIdAsync(statementId, cancellationToken);
        return VerifyStatementResult.Success(StatementMapper.ToDetailResponse(updated!));
    }

    public async Task<StatementDetailResponse?> RequestReprocessAsync(Guid statementId, Guid userId, CancellationToken cancellationToken = default)
    {
        var statement = await statementRepository.GetByIdAsync(statementId, cancellationToken);
        if (statement is null || statement.UserId != userId)
        {
            return null;
        }

        await backgroundJobScheduler.EnqueueStatementReprocessAsync(statementId, userId, cancellationToken);

        var refreshed = await statementRepository.GetByIdAsync(statementId, cancellationToken);
        return StatementMapper.ToDetailResponse(refreshed!);
    }
}
