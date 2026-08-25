using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Constants;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Application.Services;

public class StatementProcessingService(
    IStatementRepository statementRepository,
    IStatementExtractionRepository statementExtractionRepository,
    ITransactionRepository transactionRepository,
    IFileStorageService fileStorage,
    IPdfTextExtractionService pdfTextExtractionService,
    IOcrService ocrService,
    IStatementFieldExtractionService statementFieldExtractionService,
    ITransactionExtractionService transactionExtractionService,
    ITransactionClassificationService transactionClassificationService,
    IReconciliationService reconciliationService) : IStatementProcessingService
{
    public async Task<StatementDetailResponse?> ProcessAsync(Guid statementId, Guid userId, CancellationToken cancellationToken = default)
    {
        var statement = await statementRepository.GetByIdAsync(statementId, cancellationToken);
        if (statement is null || statement.UserId != userId)
        {
            return null;
        }

        string? rawText = null;
        var method = ExtractionMethod.DirectPdfText;

        if (statement.ContentType == "application/pdf")
        {
            rawText = await TryDirectPdfExtractionAsync(statement, cancellationToken);
        }

        if (rawText is null)
        {
            method = ExtractionMethod.Ocr;
            rawText = await TryOcrExtractionAsync(statement, cancellationToken);
        }

        if (rawText is null)
        {
            // Both available paths were tried and neither produced usable text.
            await statementRepository.UpdateStatusAsync(statementId, StatementProcessingStatus.ExtractionFailed, null, cancellationToken);
        }
        else
        {
            await PersistExtractionAsync(statement, method, rawText, cancellationToken);

            var fields = statementFieldExtractionService.Extract(rawText);
            await statementRepository.UpdateExtractedFieldsAsync(statementId, fields, cancellationToken);

            var referenceYear = fields.StatementPeriodStart?.Year ?? fields.StatementDate?.Year ?? DateTime.UtcNow.Year;
            var parsedTransactions = transactionExtractionService.Extract(rawText, referenceYear);
            var transactions = parsedTransactions.Select(p => ToTransactionEntity(statementId, p, method));
            await transactionRepository.ReplaceForStatementAsync(statementId, userId, transactions, cancellationToken);

            await transactionClassificationService.ClassifyStatementTransactionsAsync(statementId, userId, cancellationToken);
            await statementRepository.UpdateStatusAsync(statementId, StatementProcessingStatus.ClassificationComplete, DateTime.UtcNow, cancellationToken);

            // Deterministic balance check (never AI — requirement #20), then hand off to a human
            // regardless of the outcome: a clean reconciliation still has low-confidence
            // classifications that may need review, and a mismatch needs a human to investigate.
            await reconciliationService.ReconcileAsync(statementId, cancellationToken);
            await statementRepository.UpdateStatusAsync(statementId, StatementProcessingStatus.PendingReview, DateTime.UtcNow, cancellationToken);
        }

        var updated = await statementRepository.GetByIdAsync(statementId, cancellationToken);
        return updated is null ? null : StatementMapper.ToDetailResponse(updated);
    }

    /// <returns>The extracted raw text if it was usable, or null if direct extraction found
    /// nothing worth using — the caller should fall back to OCR.</returns>
    private async Task<string?> TryDirectPdfExtractionAsync(Statement statement, CancellationToken cancellationToken)
    {
        await using var fileStream = await fileStorage.OpenReadAsync(statement.StoredFilePath, cancellationToken);
        var extraction = pdfTextExtractionService.Extract(fileStream);
        return extraction.HasUsableText ? extraction.RawText : null;
    }

    private async Task<string?> TryOcrExtractionAsync(Statement statement, CancellationToken cancellationToken)
    {
        await using var fileStream = await fileStorage.OpenReadAsync(statement.StoredFilePath, cancellationToken);
        var ocrResult = await ocrService.ExtractTextAsync(fileStream, statement.ContentType, cancellationToken);

        if (!ocrResult.IsSuccess)
        {
            return null;
        }

        var characterCount = ocrResult.RawText.Count(c => !char.IsWhiteSpace(c));
        return characterCount >= TextExtractionThresholds.MinUsableCharactersPerPage ? ocrResult.RawText : null;
    }

    private async Task PersistExtractionAsync(Statement statement, ExtractionMethod method, string rawText, CancellationToken cancellationToken)
    {
        var characterCount = rawText.Count(c => !char.IsWhiteSpace(c));

        await statementExtractionRepository.UpsertAsync(new StatementExtraction
        {
            StatementId = statement.Id,
            ExtractionMethod = method,
            RawText = rawText,
            PageCount = method == ExtractionMethod.Ocr ? 1 : rawText.Count(c => c == '\f') + 1,
            CharacterCount = characterCount,
            HasUsableText = true
        }, cancellationToken);
    }

    private static Transaction ToTransactionEntity(Guid statementId, ParsedTransaction parsed, ExtractionMethod method) => new()
    {
        StatementId = statementId,
        TransactionDate = parsed.TransactionDate,
        PostingDate = parsed.PostingDate,
        Description = parsed.Description,
        Merchant = parsed.Merchant,
        ReferenceNumber = parsed.ReferenceNumber,
        DebitAmount = parsed.DebitAmount,
        CreditAmount = parsed.CreditAmount,
        Amount = parsed.Amount,
        Currency = parsed.Currency,
        TransactionType = parsed.TransactionType,
        Extraction = new TransactionExtraction
        {
            RawText = parsed.RawLine,
            ExtractionMethod = method
        }
    };
}
