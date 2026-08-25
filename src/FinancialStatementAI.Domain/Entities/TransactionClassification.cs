using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Domain.Entities;

/// <summary>One classification attempt for a Transaction (rule, merchant mapping, or LLM).
/// A Transaction can accumulate several of these over time (e.g. reclassified after a
/// merchant mapping is added); <see cref="IsCurrent"/> marks the one that set the
/// Transaction's live CategoryId. Never overwritten — see challenge requirement #9.</summary>
public class TransactionClassification : BaseEntity
{
    public Guid TransactionId { get; set; }
    public Transaction? Transaction { get; set; }

    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }

    public decimal ConfidenceScore { get; set; }
    public ClassificationMethod ClassificationMethod { get; set; }
    public string? Reason { get; set; }
    public bool IsCurrent { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
