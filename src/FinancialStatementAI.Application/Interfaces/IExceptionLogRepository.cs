using FinancialStatementAI.Domain.Entities;

namespace FinancialStatementAI.Application.Interfaces;

public interface IExceptionLogRepository
{
    Task AddAsync(ExceptionLog exceptionLog, CancellationToken cancellationToken = default);
}
