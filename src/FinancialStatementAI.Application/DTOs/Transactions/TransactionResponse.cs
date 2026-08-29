namespace FinancialStatementAI.Application.DTOs.Transactions;

/// <summary>A transaction as the human review UI needs it: the normalized fields, its current
/// (possibly human-corrected) category alongside how confident the classifier actually was, and
/// the full correction audit trail.</summary>
public class TransactionResponse
{
    public Guid Id { get; set; }
    public Guid StatementId { get; set; }
    public string? StatementFileName { get; set; }

    public DateOnly? TransactionDate { get; set; }
    public DateOnly? PostingDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Merchant { get; set; }
    public string? ReferenceNumber { get; set; }
    public decimal? DebitAmount { get; set; }
    public decimal? CreditAmount { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string? PageSourceLocation { get; set; }

    /// <summary>How confident the EXTRACTION step (not classification) is that this row was
    /// parsed correctly — requirement #8 lists this as a distinct confidence type. Reflects which
    /// parsing strategy produced the row (direct line match, reconstructed table, OCR cell-per-line
    /// heuristics, or a typed spreadsheet cell).</summary>
    public decimal? ExtractionConfidence { get; set; }

    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }

    /// <summary>From the current (<c>IsCurrent</c>) TransactionClassification row — null if this
    /// transaction was never classified (shouldn't happen post-Phase 10, but never guessed).</summary>
    public decimal? ClassificationConfidence { get; set; }
    public string? ClassificationMethod { get; set; }
    public string? ClassificationReason { get; set; }

    /// <summary>"HighConfidence" / "ReviewRecommended" / "ReviewRequired" — mirrors
    /// Domain.Constants.ClassificationConfidenceThresholds; null when never classified.</summary>
    public string? ReviewPriority { get; set; }

    /// <summary>True once at least one human correction exists for this transaction — lets the
    /// review UI distinguish "AI-classified, unreviewed" from "already reviewed by a human."</summary>
    public bool HasBeenCorrected { get; set; }

    public bool IsPotentialDuplicate { get; set; }
    public Guid? DuplicateOfTransactionId { get; set; }

    public IReadOnlyList<TransactionCorrectionResponse> Corrections { get; set; } = [];
}
