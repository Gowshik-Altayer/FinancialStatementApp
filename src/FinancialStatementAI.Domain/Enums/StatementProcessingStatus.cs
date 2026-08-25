namespace FinancialStatementAI.Domain.Enums;

/// <summary>Lifecycle of a Statement as it moves through the document-processing pipeline.</summary>
public enum StatementProcessingStatus
{
    Uploaded,
    Processing,
    ExtractionFailed,
    ExtractionComplete,
    ClassificationComplete,
    PendingReview,
    Verified
}
