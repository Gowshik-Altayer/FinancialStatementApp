using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Application.Interfaces;

public interface IProcessingJobRepository
{
    Task AddAsync(ProcessingJob processingJob, CancellationToken cancellationToken = default);

    /// <summary>Updates a job's status and the matching timestamp (StartedAt for Running,
    /// CompletedAt for Succeeded/Failed/Cancelled) — the audit trail requirement #22 calls for,
    /// tracked independently of whatever Hangfire's own storage says (see ProcessingJob's own
    /// doc comment).</summary>
    Task MarkStatusAsync(Guid processingJobId, ProcessingJobStatus status, CancellationToken cancellationToken = default);
}
