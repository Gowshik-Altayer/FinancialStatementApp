using FinancialStatementAI.Application.DTOs.Statements;
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

    /// <summary>Applies whichever fields <see cref="IStatementFieldExtractionService"/> managed
    /// to find — fields it couldn't find are left untouched, never overwritten with null.</summary>
    Task UpdateExtractedFieldsAsync(Guid statementId, ExtractedStatementFields fields, CancellationToken cancellationToken = default);
}
