using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Application.Services;

public class StatementService(
    IStatementFileValidator fileValidator,
    IFileStorageService fileStorage,
    IStatementRepository statementRepository,
    IProcessingJobRepository processingJobRepository) : IStatementService
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

    public async Task<IReadOnlyList<StatementSummaryResponse>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var statements = await statementRepository.GetByUserIdAsync(userId, cancellationToken);

        return statements
            .OrderByDescending(s => s.UploadedAt)
            .Select(StatementMapper.ToSummaryResponse)
            .ToList();
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
}
