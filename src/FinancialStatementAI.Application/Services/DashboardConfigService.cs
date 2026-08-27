using FinancialStatementAI.Application.DTOs.Dashboard;
using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Constants;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Application.Services;

public class DashboardConfigService(IDashboardConfigRepository repository) : IDashboardConfigService
{
    public async Task<IReadOnlyList<DashboardWidgetPreferenceResponse>> GetResolvedConfigAsync(Guid userId, UserRole role, CancellationToken cancellationToken = default)
    {
        var roleDefaults = await repository.GetRoleDefaultsAsync(role, cancellationToken);
        var userOverrides = await repository.GetUserOverridesAsync(userId, cancellationToken);

        var resolved = new Dictionary<string, DashboardWidgetPreferenceResponse>();

        foreach (var row in roleDefaults)
        {
            resolved[row.WidgetKey] = ToResponse(row, "RoleDefault");
        }

        // A user override fully replaces the role-default row for that key — no field-level
        // merging, so "visible + sort order" always comes from one consistent source per widget.
        foreach (var row in userOverrides)
        {
            resolved[row.WidgetKey] = ToResponse(row, "UserOverride");
        }

        // Any widget the registry knows about but neither layer has a row for defaults to
        // visible, sorted last — so shipping a new widget doesn't require a data migration to
        // make it appear for existing users.
        var nextSortOrder = resolved.Count > 0 ? resolved.Values.Max(w => w.SortOrder) + 1 : 0;
        foreach (var key in DashboardWidgetKeys.All)
        {
            if (!resolved.ContainsKey(key))
            {
                resolved[key] = new DashboardWidgetPreferenceResponse { WidgetKey = key, IsVisible = true, SortOrder = nextSortOrder++, Source = "SystemDefault" };
            }
        }

        return resolved.Values.OrderBy(w => w.SortOrder).ThenBy(w => w.WidgetKey).ToList();
    }

    public Task ReplaceUserOverridesAsync(Guid userId, UpdateDashboardWidgetPreferencesRequest request, CancellationToken cancellationToken = default) =>
        repository.ReplaceUserOverridesAsync(userId, ToEntities(request), cancellationToken);

    public async Task<IReadOnlyList<DashboardWidgetPreferenceResponse>> GetRoleDefaultsAsync(UserRole role, CancellationToken cancellationToken = default)
    {
        var rows = await repository.GetRoleDefaultsAsync(role, cancellationToken);
        return rows.OrderBy(r => r.SortOrder).Select(r => ToResponse(r, "RoleDefault")).ToList();
    }

    public Task ReplaceRoleDefaultsAsync(UserRole role, UpdateDashboardWidgetPreferencesRequest request, CancellationToken cancellationToken = default) =>
        repository.ReplaceRoleDefaultsAsync(role, ToEntities(request), cancellationToken);

    private static List<DashboardWidgetPreference> ToEntities(UpdateDashboardWidgetPreferencesRequest request) =>
        request.Items.Select(item => new DashboardWidgetPreference
        {
            WidgetKey = item.WidgetKey,
            IsVisible = item.IsVisible,
            SortOrder = item.SortOrder
        }).ToList();

    private static DashboardWidgetPreferenceResponse ToResponse(DashboardWidgetPreference entity, string source) => new()
    {
        WidgetKey = entity.WidgetKey,
        IsVisible = entity.IsVisible,
        SortOrder = entity.SortOrder,
        Source = source
    };
}
