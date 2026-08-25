using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Infrastructure.Persistence;

namespace FinancialStatementAI.Infrastructure.Repositories;

public class ProcessingJobRepository(AppDbContext dbContext) : IProcessingJobRepository
{
    public async Task AddAsync(ProcessingJob processingJob, CancellationToken cancellationToken = default)
    {
        dbContext.ProcessingJobs.Add(processingJob);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
