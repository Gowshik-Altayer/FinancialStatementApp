namespace FinancialStatementAI.Application.Interfaces;

/// <summary>Abstracts "how does a statement's reprocess pipeline actually get run" (requirement
/// #11/#22) behind a swappable provider, the same pattern as every other technology abstraction
/// in this codebase (IFileStorageService, IOcrService, ITransactionClassifier, ...): a default
/// that runs the work immediately/synchronously (honest, zero-config, and what every existing
/// test exercises), and a real Hangfire-backed implementation selected via
/// "BackgroundJobs:Provider" = "Hangfire" that enqueues the work for a separate worker process
/// instead. The business layer (StatementService) never depends on Hangfire directly.</summary>
public interface IBackgroundJobScheduler
{
    Task EnqueueStatementReprocessAsync(Guid statementId, Guid userId, CancellationToken cancellationToken = default);
}
