using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Domain.Enums;
using FinancialStatementAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinancialStatementAI.Infrastructure.Repositories;

public class DashboardConfigRepository(AppDbContext dbContext) : IDashboardConfigRepository
{
    public Task<IReadOnlyList<DashboardWidgetPreference>> GetRoleDefaultsAsync(UserRole role, CancellationToken cancellationToken = default) =>
        GetAsList(dbContext.DashboardWidgetPreferences.Where(p => p.Role == role), cancellationToken);

    public Task<IReadOnlyList<DashboardWidgetPreference>> GetUserOverridesAsync(Guid userId, CancellationToken cancellationToken = default) =>
        GetAsList(dbContext.DashboardWidgetPreferences.Where(p => p.UserId == userId), cancellationToken);

    public async Task ReplaceUserOverridesAsync(Guid userId, IReadOnlyList<DashboardWidgetPreference> items, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.DashboardWidgetPreferences.Where(p => p.UserId == userId).ToListAsync(cancellationToken);
        dbContext.DashboardWidgetPreferences.RemoveRange(existing);

        foreach (var item in items)
        {
            item.UserId = userId;
            item.Role = null;
            item.UpdatedAt = DateTime.UtcNow;
            dbContext.DashboardWidgetPreferences.Add(item);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplaceRoleDefaultsAsync(UserRole role, IReadOnlyList<DashboardWidgetPreference> items, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.DashboardWidgetPreferences.Where(p => p.Role == role).ToListAsync(cancellationToken);
        dbContext.DashboardWidgetPreferences.RemoveRange(existing);

        foreach (var item in items)
        {
            item.Role = role;
            item.UserId = null;
            item.UpdatedAt = DateTime.UtcNow;
            dbContext.DashboardWidgetPreferences.Add(item);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<DashboardWidgetPreference>> GetAsList(IQueryable<DashboardWidgetPreference> query, CancellationToken cancellationToken) =>
        await query.AsNoTracking().ToListAsync(cancellationToken);
}
