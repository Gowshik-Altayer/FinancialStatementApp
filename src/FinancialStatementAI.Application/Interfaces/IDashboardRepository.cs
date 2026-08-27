using FinancialStatementAI.Domain.Entities;

namespace FinancialStatementAI.Application.Interfaces;

public interface IDashboardRepository
{
    /// <summary>Every statement (with transactions/classifications/categories/corrections/
    /// reconciliation results loaded) scoped to <paramref name="userId"/>, or every user's
    /// statements when <paramref name="userId"/> is null (Admin view). One fully-loaded query
    /// rather than several repositories' worth of round trips, since DashboardService computes
    /// every KPI/chart from the same object graph in memory — matching this codebase's existing
    /// pattern (see ReconciliationService) of repositories fetching, services aggregating.</summary>
    Task<IReadOnlyList<Statement>> GetStatementsForDashboardAsync(Guid? userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TransactionCorrection>> GetRecentCorrectionsAsync(Guid? userId, int take, CancellationToken cancellationToken = default);

    /// <summary>Every user's Id/Role/IsActive — backs the Admin-only "users overview" widget.
    /// Never called for a non-Admin summary request (see DashboardService).</summary>
    Task<IReadOnlyList<User>> GetAllUsersAsync(CancellationToken cancellationToken = default);
}
