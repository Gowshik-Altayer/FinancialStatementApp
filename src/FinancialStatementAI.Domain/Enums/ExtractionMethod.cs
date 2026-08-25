namespace FinancialStatementAI.Domain.Enums;

/// <summary>Which document-processing path produced a TransactionExtraction.</summary>
public enum ExtractionMethod
{
    DirectPdfText,
    Ocr,
    VisionAi,
    DocumentIntelligence
}
