using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Domain.Entities;

/// <summary>Audit trail row for a single human-reviewed field correction on a Transaction.
/// One row per corrected field, so a reviewer changing both Merchant and Category in the same
/// review produces two rows. The original AI/extraction result is never overwritten — see
/// challenge requirement #9.</summary>
public class TransactionCorrection : BaseEntity
{
    public Guid TransactionId { get; set; }
    public Transaction? Transaction { get; set; }

    public CorrectedField FieldName { get; set; }
    public string? OriginalValue { get; set; }
    public string CorrectedValue { get; set; } = string.Empty;

    public Guid CorrectedByUserId { get; set; }
    public User? CorrectedByUser { get; set; }
    public DateTime CorrectedAt { get; set; } = DateTime.UtcNow;
    public string? CorrectionReason { get; set; }
}
