using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Domain.Entities;

/// <summary>Tracks one background-processing run of the pipeline for a Statement (backed by a
/// Hangfire job). Idempotency/retry bookkeeping lives here rather than trusting Hangfire's own
/// state store as the sole source of truth — see challenge requirement #22.</summary>
public class ProcessingJob : BaseEntity
{
    public Guid StatementId { get; set; }
    public Statement? Statement { get; set; }

    public string? HangfireJobId { get; set; }
    public ProcessingStage Stage { get; set; }
    public ProcessingJobStatus Status { get; set; } = ProcessingJobStatus.Pending;
    public int RetryCount { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ProcessingError> ProcessingErrors { get; set; } = [];
}
