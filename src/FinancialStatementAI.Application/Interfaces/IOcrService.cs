using FinancialStatementAI.Application.DTOs.Statements;

namespace FinancialStatementAI.Application.Interfaces;

/// <summary>Converts image/scanned-PDF content into text. Used when a document has no usable
/// embedded text layer (see Phase 7's decision in docs/ai-processing.md) — the business layer
/// never depends on a concrete OCR SDK directly, only on this abstraction.</summary>
public interface IOcrService
{
    Task<OcrResult> ExtractTextAsync(Stream content, string contentType, CancellationToken cancellationToken = default);
}
