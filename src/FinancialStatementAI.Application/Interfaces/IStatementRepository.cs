using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Application.Interfaces;

public interface IStatementRepository
{
    Task AddAsync(Statement statement, CancellationToken cancellationToken = default);
    Task<Statement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Statement>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task UpdateStatusAsync(
        Guid statementId,
        StatementProcessingStatus status,
        DateTime? processedAt,
        CancellationToken cancellationToken = default);
}
