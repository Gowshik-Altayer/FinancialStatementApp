using FinancialStatementAI.Domain.Entities;

namespace FinancialStatementAI.Application.Interfaces;

public interface IStatementExtractionRepository
{
    /// <summary>Inserts or replaces the (1:1) extraction row for the given StatementId — a
    /// reprocess should overwrite the previous extraction result rather than accumulate rows.
    /// <paramref name="extraction"/>'s TextBlocks/TableRegions collections (if populated) fully
    /// replace whatever was previously stored, the same "reprocess replaces, never accumulates"
    /// rule as the parent row.</summary>
    Task UpsertAsync(StatementExtraction extraction, CancellationToken cancellationToken = default);
}
