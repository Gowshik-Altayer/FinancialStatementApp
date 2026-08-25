using FinancialStatementAI.Domain.Entities;

namespace FinancialStatementAI.Application.Interfaces;

public interface IStatementExtractionRepository
{
    /// <summary>Inserts or replaces the (1:1) extraction row for the given StatementId — a
    /// reprocess should overwrite the previous extraction result rather than accumulate rows.</summary>
    Task UpsertAsync(StatementExtraction extraction, CancellationToken cancellationToken = default);
}
