using FinancialStatementAI.Domain.Entities;

namespace FinancialStatementAI.Application.Interfaces;

public interface IProcessingJobRepository
{
    Task AddAsync(ProcessingJob processingJob, CancellationToken cancellationToken = default);
}
