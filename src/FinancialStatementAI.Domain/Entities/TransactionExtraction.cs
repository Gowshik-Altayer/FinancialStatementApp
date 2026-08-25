using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Domain.Entities;

/// <summary>The raw, pre-normalization source data a Transaction was derived from — the
/// "source of truth" record used to prevent AI hallucination (never invent what isn't here).</summary>
public class TransactionExtraction : BaseEntity
{
    public Guid TransactionId { get; set; }
    public Transaction? Transaction { get; set; }

    public string RawText { get; set; } = string.Empty;
    public ExtractionMethod ExtractionMethod { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public int? PageNumber { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
