using FinancialStatementAI.Domain.Entities;

namespace FinancialStatementAI.Application.Interfaces;

public interface IReconciliationRepository
{
    /// <summary>Appends a new reconciliation run — never overwrites a prior one, so a
    /// statement's reconciliation history stays inspectable across reprocesses.</summary>
    Task AddAsync(ReconciliationResult result, CancellationToken cancellationToken = default);

    Task<ReconciliationResult?> GetLatestAsync(Guid statementId, CancellationToken cancellationToken = default);
}
