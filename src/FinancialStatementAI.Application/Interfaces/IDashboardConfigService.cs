using FinancialStatementAI.Application.DTOs.Dashboard;
using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Application.Interfaces;

public interface IDashboardConfigService
{
    Task<IReadOnlyList<DashboardWidgetPreferenceResponse>> GetResolvedConfigAsync(Guid userId, UserRole role, CancellationToken cancellationToken = default);
    Task ReplaceUserOverridesAsync(Guid userId, UpdateDashboardWidgetPreferencesRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DashboardWidgetPreferenceResponse>> GetRoleDefaultsAsync(UserRole role, CancellationToken cancellationToken = default);
    Task ReplaceRoleDefaultsAsync(UserRole role, UpdateDashboardWidgetPreferencesRequest request, CancellationToken cancellationToken = default);
}
