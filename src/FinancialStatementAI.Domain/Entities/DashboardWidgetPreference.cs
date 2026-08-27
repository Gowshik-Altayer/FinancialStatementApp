using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Domain.Entities;

/// <summary>One row of dashboard widget configuration — either a role-default (Role set, UserId
/// null) or a per-user override (UserId set, Role null). Never both, never neither (enforced by
/// a CHECK constraint in DashboardWidgetPreferenceConfiguration). WidgetKey is a free-text string
/// matching the frontend's widget registry, not an enum/FK, so a new widget never needs a
/// migration — just a new key and, optionally, new role-default seed rows.
///
/// Resolution (see IDashboardService): a user's effective dashboard is their role's default rows,
/// with any of their own override rows replacing the corresponding role-default row by
/// WidgetKey. A widget with no row at all in either layer defaults to visible, sorted last.</summary>
public class DashboardWidgetPreference : BaseEntity
{
    public UserRole? Role { get; set; }
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public string WidgetKey { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsVisible { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
