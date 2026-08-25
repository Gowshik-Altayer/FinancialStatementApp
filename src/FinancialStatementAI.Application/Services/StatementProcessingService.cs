using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Constants;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Application.Services;

public class StatementProcessingService(
    IStatementRepository statementRepository,
    IStatementExtractionRepository statementExtractionRepository,
    IFileStorageService fileStorage,
    IPdfTextExtractionService pdfTextExtractionService,
    IOcrService ocrService) : IStatementProcessingService
{
    public async Task<StatementDetailResponse?> ProcessAsync(Guid statementId, Guid userId, CancellationToken cancellationToken = default)
    {
        var statement = await statementRepository.GetByIdAsync(statementId, cancellationToken);
        if (statement is null || statement.UserId != userId)
        {
            return null;
        }

        var needsOcr = true;

        if (statement.ContentType == "application/pdf")
        {
            needsOcr = !await ExtractPdfTextAsync(statement, cancellationToken);
        }

        if (needsOcr)
        {
            await RunOcrAsync(statement, cancellationToken);
        }

        var updated = await statementRepository.GetByIdAsync(statementId, cancellationToken);
        return updated is null ? null : StatementMapper.ToDetailResponse(updated);
    }

    /// <returns>Whether the direct extraction produced usable text (see docs/ai-processing.md
    /// for the threshold and reasoning) — false means the caller should fall back to OCR.</returns>
    private async Task<bool> ExtractPdfTextAsync(Statement statement, CancellationToken cancellationToken)
    {
        await using var fileStream = await fileStorage.OpenReadAsync(statement.StoredFilePath, cancellationToken);
        var extraction = pdfTextExtractionService.Extract(fileStream);

        if (!extraction.HasUsableText)
        {
            return false;
        }

        await statementExtractionRepository.UpsertAsync(new StatementExtraction
        {
            StatementId = statement.Id,
            ExtractionMethod = ExtractionMethod.DirectPdfText,
            RawText = extraction.RawText,
            PageCount = extraction.PageCount,
            CharacterCount = extraction.CharacterCount,
            HasUsableText = true
        }, cancellationToken);

        await statementRepository.UpdateStatusAsync(statement.Id, StatementProcessingStatus.ExtractionComplete, DateTime.UtcNow, cancellationToken);
        return true;
    }

    /// <summary>Used for images outright, and as the fallback when direct PDF extraction found no
    /// usable text layer (a scanned PDF) — see requirement #2's core OCR-vs-direct decision.</summary>
    private async Task RunOcrAsync(Statement statement, CancellationToken cancellationToken)
    {
        await using var fileStream = await fileStorage.OpenReadAsync(statement.StoredFilePath, cancellationToken);
        var ocrResult = await ocrService.ExtractTextAsync(fileStream, statement.ContentType, cancellationToken);

        if (!ocrResult.IsSuccess)
        {
            await statementRepository.UpdateStatusAsync(statement.Id, StatementProcessingStatus.ExtractionFailed, null, cancellationToken);
            return;
        }

        var characterCount = ocrResult.RawText.Count(c => !char.IsWhiteSpace(c));
        var hasUsableText = characterCount >= TextExtractionThresholds.MinUsableCharactersPerPage;

        await statementExtractionRepository.UpsertAsync(new StatementExtraction
        {
            StatementId = statement.Id,
            ExtractionMethod = ExtractionMethod.Ocr,
            RawText = ocrResult.RawText,
            PageCount = 1,
            CharacterCount = characterCount,
            HasUsableText = hasUsableText
        }, cancellationToken);

        await statementRepository.UpdateStatusAsync(
            statement.Id,
            hasUsableText ? StatementProcessingStatus.ExtractionComplete : StatementProcessingStatus.ExtractionFailed,
            hasUsableText ? DateTime.UtcNow : null,
            cancellationToken);
    }
}
