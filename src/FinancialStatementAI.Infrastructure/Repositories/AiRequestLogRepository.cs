using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Infrastructure.Persistence;

namespace FinancialStatementAI.Infrastructure.Repositories;

public class AiRequestLogRepository(AppDbContext dbContext) : IAiRequestLogRepository
{
    public async Task AddAsync(AIRequest request, CancellationToken cancellationToken = default)
    {
        dbContext.AIRequests.Add(request);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
