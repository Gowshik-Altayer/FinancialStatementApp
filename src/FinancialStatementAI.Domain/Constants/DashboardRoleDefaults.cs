using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Domain.Constants;

/// <summary>Which widgets each role sees by default, and in what order — the literal source of
/// truth for the AddDashboardWidgetPreferences migration's seed data. An Admin sees everything
/// plus system oversight; a User sees their own upload/processing/transaction-volume story, none
/// of the AI-internals metrics; a Reviewer's layout leads with the review queue and confidence
/// distribution (SortOrder 0) since triaging low-confidence transactions is their job.
/// Every widget in DashboardWidgetKeys.All gets an explicit row per role (IsVisible=false for
/// ones that role doesn't see) rather than being omitted, so an Admin editing role defaults later
/// sees the complete widget list with correct toggle states, not a partial one.</summary>
public static class DashboardRoleDefaults
{
    public static readonly IReadOnlyList<(string WidgetKey, bool Visible)> Admin =
    [
        (DashboardWidgetKeys.KpiTotalStatements, true),
        (DashboardWidgetKeys.KpiInProgress, true),
        (DashboardWidgetKeys.KpiCompleted, true),
        (DashboardWidgetKeys.KpiFailed, true),
        (DashboardWidgetKeys.KpiTransactionsProcessed, true),
        (DashboardWidgetKeys.KpiTransactionsNeedingReview, true),
        (DashboardWidgetKeys.KpiReconciliationStatus, true),
        (DashboardWidgetKeys.KpiAvgConfidence, true),
        (DashboardWidgetKeys.KpiAvgProcessingTime, true),
        (DashboardWidgetKeys.PipelineStepper, true),
        (DashboardWidgetKeys.ChartProcessingStatus, true),
        (DashboardWidgetKeys.ChartProcessingTrend, true),
        (DashboardWidgetKeys.ChartTransactionCategories, true),
        (DashboardWidgetKeys.ChartConfidenceDistribution, true),
        (DashboardWidgetKeys.ChartReconciliationStatus, true),
        (DashboardWidgetKeys.ChartReviewStatistics, true),
        (DashboardWidgetKeys.RecentActivity, true),
        (DashboardWidgetKeys.WidgetUsersOverview, true)
    ];

    public static readonly IReadOnlyList<(string WidgetKey, bool Visible)> User =
    [
        (DashboardWidgetKeys.KpiTotalStatements, true),
        (DashboardWidgetKeys.KpiInProgress, true),
        (DashboardWidgetKeys.KpiCompleted, true),
        (DashboardWidgetKeys.KpiFailed, false),
        (DashboardWidgetKeys.KpiTransactionsProcessed, true),
        (DashboardWidgetKeys.KpiTransactionsNeedingReview, false),
        (DashboardWidgetKeys.KpiReconciliationStatus, true),
        (DashboardWidgetKeys.KpiAvgConfidence, false),
        (DashboardWidgetKeys.KpiAvgProcessingTime, false),
        (DashboardWidgetKeys.PipelineStepper, true),
        (DashboardWidgetKeys.ChartProcessingStatus, true),
        (DashboardWidgetKeys.ChartProcessingTrend, false),
        (DashboardWidgetKeys.ChartTransactionCategories, true),
        (DashboardWidgetKeys.ChartConfidenceDistribution, false),
        (DashboardWidgetKeys.ChartReconciliationStatus, true),
        (DashboardWidgetKeys.ChartReviewStatistics, false),
        (DashboardWidgetKeys.RecentActivity, true),
        (DashboardWidgetKeys.WidgetUsersOverview, false)
    ];

    public static readonly IReadOnlyList<(string WidgetKey, bool Visible)> Reviewer =
    [
        (DashboardWidgetKeys.KpiTransactionsNeedingReview, true), // leads the layout, SortOrder 0
        (DashboardWidgetKeys.ChartConfidenceDistribution, true), // leads the layout, SortOrder 1
        (DashboardWidgetKeys.KpiTotalStatements, true),
        (DashboardWidgetKeys.KpiInProgress, true),
        (DashboardWidgetKeys.KpiCompleted, false),
        (DashboardWidgetKeys.KpiFailed, false),
        (DashboardWidgetKeys.KpiTransactionsProcessed, false),
        (DashboardWidgetKeys.KpiReconciliationStatus, false),
        (DashboardWidgetKeys.KpiAvgConfidence, true),
        (DashboardWidgetKeys.KpiAvgProcessingTime, false),
        (DashboardWidgetKeys.PipelineStepper, true),
        (DashboardWidgetKeys.ChartProcessingStatus, false),
        (DashboardWidgetKeys.ChartProcessingTrend, false),
        (DashboardWidgetKeys.ChartTransactionCategories, false),
        (DashboardWidgetKeys.ChartReconciliationStatus, false),
        (DashboardWidgetKeys.ChartReviewStatistics, true),
        (DashboardWidgetKeys.RecentActivity, true),
        (DashboardWidgetKeys.WidgetUsersOverview, false)
    ];

    public static IReadOnlyList<(string WidgetKey, bool Visible)> For(UserRole role) => role switch
    {
        UserRole.Admin => Admin,
        UserRole.Reviewer => Reviewer,
        _ => User
    };
}
