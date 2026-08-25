using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Infrastructure.BackgroundJobs;

/// <summary>Default IBackgroundJobScheduler: runs the reprocess pipeline synchronously, in the
/// same request, the same way every phase before this one did — zero configuration required,
/// and what every existing test exercises. Still records a ProcessingJob row (Succeeded/Failed)
/// so the audit trail behaves consistently whichever provider is active.</summary>
public class ImmediateBackgroundJobScheduler(
    IStatementProcessingService processingService,
    IProcessingJobRepository processingJobRepository) : IBackgroundJobScheduler
{
    public async Task EnqueueStatementReprocessAsync(Guid statementId, Guid userId, CancellationToken cancellationToken = default)
    {
        var job = new ProcessingJob
        {
            StatementId = statementId,
            Stage = ProcessingStage.TextExtraction,
            Status = ProcessingJobStatus.Running,
            StartedAt = DateTime.UtcNow
        };
        await processingJobRepository.AddAsync(job, cancellationToken);

        try
        {
            await processingService.ProcessAsync(statementId, userId, cancellationToken);
            await processingJobRepository.MarkStatusAsync(job.Id, ProcessingJobStatus.Succeeded, cancellationToken);
        }
        catch
        {
            await processingJobRepository.MarkStatusAsync(job.Id, ProcessingJobStatus.Failed, cancellationToken);
            throw;
        }
    }
}
