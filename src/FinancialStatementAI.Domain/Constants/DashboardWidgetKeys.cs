namespace FinancialStatementAI.Domain.Constants;

/// <summary>The full set of dashboard widget keys this app currently knows how to render — the
/// single source of truth both the migration's seed data and DashboardConfigService's
/// "unlisted widget defaults to visible" fallback resolve against. Adding a new widget later
/// means adding a key here (plus, typically, a role-default seed row) — never a schema change,
/// since DashboardWidgetPreference.WidgetKey is a free-text column, not an enum/FK.</summary>
public static class DashboardWidgetKeys
{
    public const string KpiTotalStatements = "kpi-total-statements";
    public const string KpiInProgress = "kpi-in-progress";
    public const string KpiCompleted = "kpi-completed";
    public const string KpiFailed = "kpi-failed";
    public const string KpiTransactionsProcessed = "kpi-transactions-processed";
    public const string KpiTransactionsNeedingReview = "kpi-transactions-needing-review";
    public const string KpiReconciliationStatus = "kpi-reconciliation-status";
    public const string KpiAvgConfidence = "kpi-avg-confidence";
    public const string KpiAvgProcessingTime = "kpi-avg-processing-time";
    public const string PipelineStepper = "pipeline-stepper";
    public const string ChartProcessingStatus = "chart-processing-status";
    public const string ChartProcessingTrend = "chart-processing-trend";
    public const string ChartTransactionCategories = "chart-transaction-categories";
    public const string ChartConfidenceDistribution = "chart-confidence-distribution";
    public const string ChartReconciliationStatus = "chart-reconciliation-status";
    public const string ChartReviewStatistics = "chart-review-statistics";
    public const string RecentActivity = "recent-activity";
    public const string WidgetUsersOverview = "widget-users-overview";

    public static readonly IReadOnlyList<string> All =
    [
        KpiTotalStatements, KpiInProgress, KpiCompleted, KpiFailed, KpiTransactionsProcessed,
        KpiTransactionsNeedingReview, KpiReconciliationStatus, KpiAvgConfidence, KpiAvgProcessingTime,
        PipelineStepper, ChartProcessingStatus, ChartProcessingTrend, ChartTransactionCategories,
        ChartConfidenceDistribution, ChartReconciliationStatus, ChartReviewStatistics,
        RecentActivity, WidgetUsersOverview
    ];
}
