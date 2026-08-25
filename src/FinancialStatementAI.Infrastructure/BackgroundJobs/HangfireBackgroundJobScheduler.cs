using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Domain.Enums;
using Hangfire;

namespace FinancialStatementAI.Infrastructure.BackgroundJobs;

/// <summary>The real Hangfire-backed IBackgroundJobScheduler, active when
/// "BackgroundJobs:Provider" = "Hangfire". Enqueues the actual pipeline work
/// (IStatementProcessingService.ProcessAsync) for a separate worker process to pick up —
/// resolved from Hangfire's own DI-aware job activator at execution time, not captured as a
/// closure over an injected instance, which is what lets the call survive being serialized and
/// run later/elsewhere. Immediately flips the statement to Processing and records a Pending
/// ProcessingJob row (with Hangfire's own job id) so the caller's response — and any client
/// polling GET /api/statements/{id}/status afterward — can tell the work hasn't finished yet.</summary>
public class HangfireBackgroundJobScheduler(
    IBackgroundJobClient backgroundJobClient,
    IStatementRepository statementRepository,
    IProcessingJobRepository processingJobRepository) : IBackgroundJobScheduler
{
    public async Task EnqueueStatementReprocessAsync(Guid statementId, Guid userId, CancellationToken cancellationToken = default)
    {
        await statementRepository.UpdateStatusAsync(statementId, StatementProcessingStatus.Processing, null, cancellationToken);

        var hangfireJobId = backgroundJobClient.Enqueue<IStatementProcessingService>(
            service => service.ProcessAsync(statementId, userId, CancellationToken.None));

        await processingJobRepository.AddAsync(new ProcessingJob
        {
            StatementId = statementId,
            HangfireJobId = hangfireJobId,
            Stage = ProcessingStage.TextExtraction,
            Status = ProcessingJobStatus.Pending
        }, cancellationToken);
    }
}
