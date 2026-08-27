using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Application.Interfaces;

public interface IDashboardConfigRepository
{
    Task<IReadOnlyList<DashboardWidgetPreference>> GetRoleDefaultsAsync(UserRole role, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DashboardWidgetPreference>> GetUserOverridesAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Replaces the full set of a user's own override rows with <paramref name="items"/>
    /// — a user only ever edits their own dashboard, never a role default (see
    /// UpsertRoleDefaultsAsync for that, which is Admin-only at the controller level).</summary>
    Task ReplaceUserOverridesAsync(Guid userId, IReadOnlyList<DashboardWidgetPreference> items, CancellationToken cancellationToken = default);

    Task ReplaceRoleDefaultsAsync(UserRole role, IReadOnlyList<DashboardWidgetPreference> items, CancellationToken cancellationToken = default);
}
