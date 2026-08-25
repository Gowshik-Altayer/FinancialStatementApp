using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Application.Services;

public class StatementProcessingService(
    IStatementRepository statementRepository,
    IStatementExtractionRepository statementExtractionRepository,
    IFileStorageService fileStorage,
    IPdfTextExtractionService pdfTextExtractionService) : IStatementProcessingService
{
    public async Task<StatementDetailResponse?> ProcessAsync(Guid statementId, Guid userId, CancellationToken cancellationToken = default)
    {
        var statement = await statementRepository.GetByIdAsync(statementId, cancellationToken);
        if (statement is null || statement.UserId != userId)
        {
            return null;
        }

        if (statement.ContentType == "application/pdf")
        {
            await ExtractPdfTextAsync(statement, cancellationToken);
        }
        else
        {
            // Images have no embedded text layer to extract directly — they need OCR/Vision
            // (Phase 8). Leave status at Processing rather than claiming completion here.
            await statementRepository.UpdateStatusAsync(statementId, StatementProcessingStatus.Processing, null, cancellationToken);
        }

        var updated = await statementRepository.GetByIdAsync(statementId, cancellationToken);
        return updated is null ? null : StatementMapper.ToDetailResponse(updated);
    }

    private async Task ExtractPdfTextAsync(Statement statement, CancellationToken cancellationToken)
    {
        await using var fileStream = await fileStorage.OpenReadAsync(statement.StoredFilePath, cancellationToken);
        var extraction = pdfTextExtractionService.Extract(fileStream);

        await statementExtractionRepository.UpsertAsync(new StatementExtraction
        {
            StatementId = statement.Id,
            ExtractionMethod = ExtractionMethod.DirectPdfText,
            RawText = extraction.RawText,
            PageCount = extraction.PageCount,
            CharacterCount = extraction.CharacterCount,
            HasUsableText = extraction.HasUsableText
        }, cancellationToken);

        var newStatus = extraction.HasUsableText
            ? StatementProcessingStatus.ExtractionComplete
            : StatementProcessingStatus.Processing; // insufficient text — awaiting OCR (Phase 8)

        await statementRepository.UpdateStatusAsync(
            statement.Id,
            newStatus,
            extraction.HasUsableText ? DateTime.UtcNow : null,
            cancellationToken);
    }
}
