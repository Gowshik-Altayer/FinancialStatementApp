using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Infrastructure.Persistence;

namespace FinancialStatementAI.Infrastructure.Repositories;

public class ExceptionLogRepository(AppDbContext dbContext) : IExceptionLogRepository
{
    public async Task AddAsync(ExceptionLog exceptionLog, CancellationToken cancellationToken = default)
    {
        dbContext.ExceptionLogs.Add(exceptionLog);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
