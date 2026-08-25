using FinancialStatementAI.Application.DTOs.Common;
using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Application.Interfaces;

public interface IStatementRepository
{
    Task AddAsync(Statement statement, CancellationToken cancellationToken = default);
    Task<Statement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Searches/filters/paginates entirely in the database and projects straight to
    /// <see cref="StatementSummaryResponse"/> — deliberately never <c>.Include()</c>s
    /// Transactions just to count them (a documented Phase 6 tradeoff); <c>TransactionCount</c>
    /// and the latest reconciliation status are both computed via SQL subqueries instead.</summary>
    Task<PagedResult<StatementSummaryResponse>> SearchForUserAsync(
        Guid userId,
        string? search,
        StatementProcessingStatus? status,
        ReconciliationStatus? reconciliationStatus,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task UpdateStatusAsync(
        Guid statementId,
        StatementProcessingStatus status,
        DateTime? processedAt,
        CancellationToken cancellationToken = default);

    /// <summary>Applies whichever fields <see cref="IStatementFieldExtractionService"/> managed
    /// to find — fields it couldn't find are left untouched, never overwritten with null.</summary>
    Task UpdateExtractedFieldsAsync(Guid statementId, ExtractedStatementFields fields, CancellationToken cancellationToken = default);
}
