using FinancialStatementAI.Domain.Entities;

namespace FinancialStatementAI.Application.Interfaces;

public interface ICategoryRepository
{
    Task<IReadOnlyList<Category>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<Category?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
}
