namespace FinancialStatementAI.Application.DTOs.Dashboard;

public class DashboardSummaryResponse
{
    public DashboardKpis Kpis { get; set; } = new();
    public IReadOnlyList<PipelineStageResponse> PipelineStages { get; set; } = [];
    public IReadOnlyList<NamedCountResponse> ProcessingStatusBreakdown { get; set; } = [];
    public IReadOnlyList<DailyTrendPointResponse> ProcessingTrend { get; set; } = [];
    public IReadOnlyList<CategoryBreakdownResponse> TransactionsByCategory { get; set; } = [];
    public IReadOnlyList<NamedCountResponse> ConfidenceDistribution { get; set; } = [];
    public IReadOnlyList<NamedCountResponse> ReconciliationStatusBreakdown { get; set; } = [];
    public ReviewStatisticsResponse ReviewStatistics { get; set; } = new();
    public IReadOnlyList<ActivityItemResponse> RecentActivity { get; set; } = [];

    /// <summary>Null for a non-Admin request — this is system-oversight data, not something a
    /// regular user's own dashboard should ever compute or expose.</summary>
    public UsersOverviewResponse? UsersOverview { get; set; }
}

public class UsersOverviewResponse
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public IReadOnlyList<NamedCountResponse> RoleBreakdown { get; set; } = [];
}

public class DashboardKpis
{
    public int TotalStatements { get; set; }
    public int CompletedCount { get; set; }
    public int InProgressCount { get; set; }
    public int FailedCount { get; set; }
    public int TransactionsProcessed { get; set; }
    public int TransactionsNeedingReview { get; set; }
    public int ReconciledCount { get; set; }
    public int MismatchCount { get; set; }
    public int PendingReconciliationCount { get; set; }

    /// <summary>Average of every current TransactionClassification.ConfidenceScore across the
    /// user's transactions — null when there's nothing classified yet, never fabricated as 0.</summary>
    public decimal? AverageClassificationConfidence { get; set; }

    /// <summary>Average (ProcessedAt - UploadedAt) in seconds over statements that have actually
    /// finished (ProcessedAt is set) — null when nothing has completed yet.</summary>
    public double? AverageProcessingTimeSeconds { get; set; }
}

/// <summary>One box on the 8-stage pipeline diagram. Count is a FUNNEL count — "how many of the
/// user's statements have reached at least this stage" — not "how many are executing this exact
/// stage right this second": this app's default synchronous pipeline doesn't persist per-stage
/// progress markers (see DashboardService's own doc comment for why), so a funnel view is what
/// the real data actually supports honestly.</summary>
public class PipelineStageResponse
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }

    /// <summary>"pending" | "in-progress" | "complete" | "failed" — drives the stepper's color;
    /// "failed" only ever applies to a stage where the count includes statements this specific
    /// stage's own logic rejected (currently only meaningful for the terminal "Completed" box
    /// showing FailedCount separately isn't attempted here — see PipelineStageMapper).</summary>
    public string State { get; set; } = "pending";
}

public class NamedCountResponse
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class DailyTrendPointResponse
{
    public DateOnly Date { get; set; }
    public int UploadedCount { get; set; }
    public int CompletedCount { get; set; }
    public int FailedCount { get; set; }
}

public class CategoryBreakdownResponse
{
    public string CategoryName { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
    public decimal TotalAmount { get; set; }
}

public class ReviewStatisticsResponse
{
    public int PendingCount { get; set; }
    public int CorrectedCount { get; set; }
    public int AiAcceptedCount { get; set; }
}

public class ActivityItemResponse
{
    public string Type { get; set; } = string.Empty; // "Upload" | "Completed" | "Failed" | "Correction"
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Guid? StatementId { get; set; }
}
