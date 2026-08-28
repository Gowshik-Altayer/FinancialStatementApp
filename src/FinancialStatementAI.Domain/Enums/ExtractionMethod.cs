namespace FinancialStatementAI.Domain.Enums;

/// <summary>Which document-processing path produced a TransactionExtraction.</summary>
public enum ExtractionMethod
{
    DirectPdfText,
    Ocr,
    VisionAi,
    DocumentIntelligence,
    /// <summary>Read directly from a structured spreadsheet (.xlsx) rather than extracted from
    /// text or an image — see SpreadsheetTransactionExtractionService. There is no OCR/text-layer
    /// step for this path at all: cells are read by column header.</summary>
    Spreadsheet
}
