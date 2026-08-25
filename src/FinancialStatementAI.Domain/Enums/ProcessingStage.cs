namespace FinancialStatementAI.Domain.Enums;

/// <summary>Stage of the document-processing pipeline, used for job tracking, error attribution, and logging.</summary>
public enum ProcessingStage
{
    Upload,
    Validation,
    TextExtraction,
    Ocr,
    TransactionExtraction,
    Normalization,
    Classification,
    Reconciliation,
    Review
}
