using FinancialStatementAI.Application.DTOs.Statements;

namespace FinancialStatementAI.Application.Interfaces;

/// <summary>Extracts machine-readable text directly from a digital PDF's text layer — no OCR/
/// Vision involved. See <see cref="IOcrService"/> (Phase 8) for the scanned-document path.</summary>
public interface IPdfTextExtractionService
{
    PdfExtractionResult Extract(Stream pdfContent);
}
