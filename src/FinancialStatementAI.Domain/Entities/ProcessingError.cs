using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Domain.Entities;

/// <summary>A single recoverable failure during processing — statement-level or scoped to one
/// transaction. Recording these (instead of failing the whole pipeline) is what lets one bad
/// transaction not take down the rest of the statement — see requirement #14.</summary>
public class ProcessingError : BaseEntity
{
    public Guid StatementId { get; set; }
    public Statement? Statement { get; set; }

    public Guid? ProcessingJobId { get; set; }
    public ProcessingJob? ProcessingJob { get; set; }

    public Guid? TransactionId { get; set; }
    public Transaction? Transaction { get; set; }

    public ProcessingStage Stage { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string? ErrorDetails { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
