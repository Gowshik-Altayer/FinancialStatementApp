using FinancialStatementAI.Domain.Entities;

namespace FinancialStatementAI.Application.Interfaces;

public interface IStatementRepository
{
    Task AddAsync(Statement statement, CancellationToken cancellationToken = default);
    Task<Statement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Statement>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
