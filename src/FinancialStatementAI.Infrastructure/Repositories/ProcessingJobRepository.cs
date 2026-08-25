using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Domain.Enums;
using FinancialStatementAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinancialStatementAI.Infrastructure.Repositories;

public class ProcessingJobRepository(AppDbContext dbContext) : IProcessingJobRepository
{
    public async Task AddAsync(ProcessingJob processingJob, CancellationToken cancellationToken = default)
    {
        dbContext.ProcessingJobs.Add(processingJob);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkStatusAsync(Guid processingJobId, ProcessingJobStatus status, CancellationToken cancellationToken = default)
    {
        var job = await dbContext.ProcessingJobs.SingleAsync(j => j.Id == processingJobId, cancellationToken);
        job.Status = status;

        switch (status)
        {
            case ProcessingJobStatus.Running:
                job.StartedAt = DateTime.UtcNow;
                break;
            case ProcessingJobStatus.Succeeded:
            case ProcessingJobStatus.Failed:
            case ProcessingJobStatus.Cancelled:
                job.CompletedAt = DateTime.UtcNow;
                break;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
