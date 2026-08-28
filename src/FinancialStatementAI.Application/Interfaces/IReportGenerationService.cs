using FinancialStatementAI.Application.DTOs.Transactions;
using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Application.Interfaces;

/// <summary>Renders one of the five data areas (Statements, Transactions, Review, Reconciliation,
/// Categories) as a downloadable XLSX or PDF report — scoped to one user's own data exactly like
/// the underlying list endpoints, and reusing their filters rather than inventing new ones. Each
/// report contains every matching row, not just one page of it (the underlying services are
/// paginated for on-screen display; report generation pages through them internally).</summary>
public interface IReportGenerationService
{
    Task<byte[]> GenerateStatementsReportAsync(
        Guid userId,
        string? search,
        Domain.Enums.StatementProcessingStatus? status,
        ReconciliationStatus? reconciliationStatus,
        ReportFormat format,
        CancellationToken cancellationToken = default);

    Task<byte[]> GenerateTransactionsReportAsync(
        Guid userId,
        TransactionSearchFilter filter,
        ReportFormat format,
        CancellationToken cancellationToken = default);

    Task<byte[]> GenerateReviewQueueReportAsync(Guid userId, ReportFormat format, CancellationToken cancellationToken = default);

    Task<byte[]> GenerateReconciliationReportAsync(
        Guid userId,
        ReconciliationStatus? status,
        string? search,
        ReportFormat format,
        CancellationToken cancellationToken = default);

    Task<byte[]> GenerateCategoriesReportAsync(Guid userId, ReportFormat format, CancellationToken cancellationToken = default);
}
