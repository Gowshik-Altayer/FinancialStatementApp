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
    IDocumentIntelligenceService documentIntelligenceService,
    IStatementFieldExtractionService statementFieldExtractionService,
    ITransactionExtractionService transactionExtractionService,
    ITransactionClassificationService transactionClassificationService,
    IReconciliationService reconciliationService,
    IDistributedLockService distributedLockService) : IStatementProcessingService
{
    private static readonly TimeSpan ProcessingLockExpiry = TimeSpan.FromMinutes(10);

    public async Task<StatementDetailResponse?> ProcessAsync(Guid statementId, Guid userId, CancellationToken cancellationToken = default)
    {
        var statement = await statementRepository.GetByIdAsync(statementId, cancellationToken);
        if (statement is null || statement.UserId != userId)
        {
            return null;
        }

        // Guards the entire pipeline below (extraction through reconciliation) against a second,
        // overlapping run for the *same* statement — a real risk now that Phase 14 makes this
        // callable from a background worker: a double-clicked Reprocess button, or two Hangfire
        // attempts racing, could otherwise both call ReplaceForStatementAsync concurrently and
        // interleave their writes. Refuses to run a second pass rather than risk that, returning
        // the (unchanged) current state instead — the in-flight run will still get there.
        await using var processingLock = await distributedLockService.TryAcquireAsync(
            $"statement-processing:{statementId}", ProcessingLockExpiry, cancellationToken);
        if (processingLock is null)
        {
            return StatementMapper.ToDetailResponse(statement);
        }

        string? rawText = null;
        var method = ExtractionMethod.DirectPdfText;
        OcrResult? ocrResult = null;
        DocumentIntelligenceResult? structureResult = null;

        if (statement.ContentType == "application/pdf")
        {
            rawText = await TryDirectPdfExtractionAsync(statement, cancellationToken);
        }

        if (rawText is null)
        {
            method = ExtractionMethod.Ocr;
            (rawText, ocrResult) = await TryOcrExtractionAsync(statement, cancellationToken);

            // Table/layout reconstruction (PP-StructureV3, when PaddleOcr is the configured
            // provider) only runs for the OCR path — a scanned document is exactly the case
            // where reconstructing a transaction table's structure earns its cost; direct PDF
            // text extraction already has clean, ordered text with no layout ambiguity to
            // resolve. A failure here is never fatal to the pipeline (see requirement #16): it
            // just means no table regions get stored alongside this extraction.
            if (rawText is not null)
            {
                structureResult = await TryDocumentStructureAnalysisAsync(statement, cancellationToken);
            }
        }

        if (rawText is null)
        {
            // Both available paths were tried and neither produced usable text.
            await statementRepository.UpdateStatusAsync(statementId, StatementProcessingStatus.ExtractionFailed, null, cancellationToken);
        }
        else
        {
            await PersistExtractionAsync(statement, method, rawText, ocrResult, structureResult, cancellationToken);

            var fields = statementFieldExtractionService.Extract(rawText);
            await statementRepository.UpdateExtractedFieldsAsync(statementId, fields, cancellationToken);

            var referenceYear = fields.StatementPeriodStart?.Year ?? fields.StatementDate?.Year ?? DateTime.UtcNow.Year;
            var parsedTransactions = ExtractTransactions(rawText, structureResult, referenceYear);
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

    /// <returns>The usable raw text (or null) alongside the full OcrResult, so the caller can
    /// persist confidence/bounding-box detail even though only the text feeds the rest of the
    /// pipeline.</returns>
    private async Task<(string? RawText, OcrResult Result)> TryOcrExtractionAsync(Statement statement, CancellationToken cancellationToken)
    {
        await using var fileStream = await fileStorage.OpenReadAsync(statement.StoredFilePath, cancellationToken);
        var ocrResult = await ocrService.ExtractTextAsync(fileStream, statement.ContentType, cancellationToken);

        if (!ocrResult.IsSuccess)
        {
            return (null, ocrResult);
        }

        var characterCount = ocrResult.RawText.Count(c => !char.IsWhiteSpace(c));
        var isUsable = characterCount >= TextExtractionThresholds.MinUsableCharactersPerPage;
        return (isUsable ? ocrResult.RawText : null, ocrResult);
    }

    /// <summary>Prefers PP-StructureV3's reconstructed table HTML over the raw-text line parser
    /// when one is available: OCR'd plain text routinely puts every table cell on its own line
    /// (one date, then one description, then one amount, ...), which the line-based parser can
    /// never reassemble into rows since it requires a date and an amount together on one line —
    /// the table's actual row/column structure is exactly what's needed there instead. Direct PDF
    /// text extraction never has a structureResult (see ProcessAsync), so it always uses the
    /// line-based parser, which is what its already-clean line-per-transaction text expects.</summary>
    private IReadOnlyList<ParsedTransaction> ExtractTransactions(string rawText, DocumentIntelligenceResult? structureResult, int referenceYear)
    {
        var tableHtml = structureResult?.Tables?.FirstOrDefault()?.Html;
        if (!string.IsNullOrWhiteSpace(tableHtml))
        {
            var fromTable = transactionExtractionService.ExtractFromTable(tableHtml, referenceYear);
            if (fromTable.Count > 0)
            {
                return fromTable;
            }
        }

        var fromLines = transactionExtractionService.Extract(rawText, referenceYear);
        if (fromLines.Count > 0)
        {
            return fromLines;
        }

        // Last resort, and the one that matters for scanned statements in practice. The line-based
        // parser needs a date and an amount on the SAME line, which OCR'd tabular text never has —
        // PP-OCRv6 reads region by region, so every cell lands on its own line. Previously that
        // combination (OCR text + no PP-StructureV3 table, which is the normal state whenever the
        // structure pipeline is disabled or has run out of memory) silently produced zero
        // transactions. Reached only when neither of the strategies above found anything, so it
        // cannot change the result for a statement that already parsed.
        return transactionExtractionService.ExtractFromCellPerLineText(rawText, referenceYear);
    }

    private async Task<DocumentIntelligenceResult?> TryDocumentStructureAnalysisAsync(Statement statement, CancellationToken cancellationToken)
    {
        try
        {
            await using var fileStream = await fileStorage.OpenReadAsync(statement.StoredFilePath, cancellationToken);
            var result = await documentIntelligenceService.AnalyzeAsync(fileStream, statement.ContentType, cancellationToken);
            return result.IsSuccess ? result : null;
        }
        catch
        {
            // Document-layout analysis is a nice-to-have enrichment of this extraction, never a
            // gate on the pipeline continuing — an unhandled exception here (e.g. the
            // PaddleOCR structure service being unreachable) must not fail the whole reprocess.
            return null;
        }
    }

    private async Task PersistExtractionAsync(
        Statement statement,
        ExtractionMethod method,
        string rawText,
        OcrResult? ocrResult,
        DocumentIntelligenceResult? structureResult,
        CancellationToken cancellationToken)
    {
        var characterCount = rawText.Count(c => !char.IsWhiteSpace(c));

        var textBlocks = ocrResult?.TextBlocks?
            .Select(b => new OcrTextBlock
            {
                PageNumber = b.PageNumber,
                Text = b.Text,
                Confidence = b.Confidence,
                X1 = b.X1,
                Y1 = b.Y1,
                X2 = b.X2,
                Y2 = b.Y2
            })
            .ToList() ?? [];

        var tableRegions = structureResult?.Tables?
            .Select(t => new OcrTableRegion
            {
                PageNumber = t.PageNumber,
                Html = t.Html,
                Confidence = t.Confidence,
                X1 = t.X1,
                Y1 = t.Y1,
                X2 = t.X2,
                Y2 = t.Y2
            })
            .ToList() ?? [];

        await statementExtractionRepository.UpsertAsync(new StatementExtraction
        {
            StatementId = statement.Id,
            ExtractionMethod = method,
            RawText = rawText,
            PageCount = method == ExtractionMethod.Ocr ? 1 : rawText.Count(c => c == '\f') + 1,
            CharacterCount = characterCount,
            HasUsableText = true,
            ConfidenceScore = ocrResult?.ConfidenceScore,
            TextBlocks = textBlocks,
            TableRegions = tableRegions
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
