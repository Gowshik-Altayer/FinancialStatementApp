using FinancialStatementAI.Application.DTOs.Dashboard;

namespace FinancialStatementAI.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardSummaryResponse> GetSummaryAsync(Guid userId, bool isAdmin, int rangeDays, CancellationToken cancellationToken = default);
}
