using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Domain.Entities;

/// <summary>The normalized transaction, in a consistent internal representation regardless of
/// the original statement's layout/format. <see cref="TransactionExtraction"/> preserves the raw
/// source text this was derived from.</summary>
public class Transaction : BaseEntity
{
    public Guid StatementId { get; set; }
    public Statement? Statement { get; set; }

    public DateOnly? TransactionDate { get; set; }
    public DateOnly? PostingDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Merchant { get; set; }
    public string? ReferenceNumber { get; set; }
    public decimal? DebitAmount { get; set; }
    public decimal? CreditAmount { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public TransactionType TransactionType { get; set; } = TransactionType.Other;
    public string? PageSourceLocation { get; set; }

    // Current effective category — reflects the latest classification, or a user correction if one exists.
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }

    // Duplicate detection (see challenge requirement #21): flagged, never auto-deleted.
    public bool IsPotentialDuplicate { get; set; }
    public Guid? DuplicateOfTransactionId { get; set; }
    public Transaction? DuplicateOfTransaction { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public TransactionExtraction? Extraction { get; set; }
    public ICollection<TransactionClassification> Classifications { get; set; } = [];
    public ICollection<TransactionCorrection> Corrections { get; set; } = [];
    public ICollection<ProcessingError> ProcessingErrors { get; set; } = [];
}
