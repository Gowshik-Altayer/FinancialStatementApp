namespace FinancialStatementAI.Application.DTOs.Dashboard;

public class DashboardWidgetPreferenceResponse
{
    public string WidgetKey { get; set; } = string.Empty;
    public bool IsVisible { get; set; }
    public int SortOrder { get; set; }

    /// <summary>"UserOverride" | "RoleDefault" | "SystemDefault" — lets the frontend show "this
    /// is your own customization" vs. "this is the default for your role" if it ever wants to.</summary>
    public string Source { get; set; } = "SystemDefault";
}

public class UpdateDashboardWidgetPreferencesRequest
{
    public List<WidgetPreferenceItem> Items { get; set; } = [];
}

public class WidgetPreferenceItem
{
    public string WidgetKey { get; set; } = string.Empty;
    public bool IsVisible { get; set; }
    public int SortOrder { get; set; }
}
