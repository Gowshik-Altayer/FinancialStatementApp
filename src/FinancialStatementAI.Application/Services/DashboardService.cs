using FinancialStatementAI.Application.DTOs.Dashboard;
using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Application.Services;

/// <summary>Computes every Dashboard KPI/chart from data that already exists — no new business
/// data is invented here, only aggregated (requirement: "do not hardcode dashboard data").
///
/// One deliberate simplification, forced by how this app's pipeline actually runs: the default
/// "BackgroundJobs:Provider=Immediate" execution path (see StatementProcessingService) runs every
/// stage synchronously in one call and never persists a per-stage progress marker — ProcessingJob
/// rows only ever get created twice per statement (at upload, and again if reprocessed), tagged
/// with a stage but not tracking "now entering OCR, now entering Classification" as it happens.
/// So "how many statements are executing exactly this stage right now" isn't observable data.
/// What IS honestly observable is "how many statements have reached at least this stage" — a
/// funnel count derived from Statement.ProcessingStatus and StatementExtraction.ExtractionMethod
/// — which is what PipelineStages below actually computes.</summary>
public class DashboardService(IDashboardRepository dashboardRepository) : IDashboardService
{
    private const int RecentActivityCount = 15;

    public async Task<DashboardSummaryResponse> GetSummaryAsync(Guid userId, bool isAdmin, int rangeDays, CancellationToken cancellationToken = default)
    {
        var scopeUserId = isAdmin ? (Guid?)null : userId;
        var statements = await dashboardRepository.GetStatementsForDashboardAsync(scopeUserId, cancellationToken);
        var transactions = statements.SelectMany(s => s.Transactions).ToList();
        var recentCorrections = await dashboardRepository.GetRecentCorrectionsAsync(scopeUserId, RecentActivityCount, cancellationToken);

        // System-oversight data — only ever computed for an Admin request, never exposed to (or
        // even queried for) a regular user's own dashboard.
        var usersOverview = isAdmin ? await BuildUsersOverviewAsync(cancellationToken) : null;

        return new DashboardSummaryResponse
        {
            UsersOverview = usersOverview,
            Kpis = BuildKpis(statements, transactions),
            PipelineStages = BuildPipelineStages(statements),
            ProcessingStatusBreakdown = BuildProcessingStatusBreakdown(statements),
            ProcessingTrend = BuildProcessingTrend(statements, rangeDays),
            TransactionsByCategory = BuildCategoryBreakdown(transactions),
            ConfidenceDistribution = BuildConfidenceDistribution(transactions),
            ReconciliationStatusBreakdown = BuildReconciliationStatusBreakdown(statements),
            ReviewStatistics = BuildReviewStatistics(transactions),
            RecentActivity = BuildRecentActivity(statements, recentCorrections)
        };
    }

    private static DashboardKpis BuildKpis(IReadOnlyList<Statement> statements, IReadOnlyList<Transaction> transactions)
    {
        var currentReconciliations = statements
            .Select(s => s.ReconciliationResults.OrderByDescending(r => r.CreatedAt).FirstOrDefault())
            .ToList();

        var currentConfidences = transactions
            .Select(t => t.Classifications.FirstOrDefault(c => c.IsCurrent)?.ConfidenceScore)
            .Where(c => c is not null)
            .Select(c => c!.Value)
            .ToList();

        var completedDurations = statements
            .Where(s => s.ProcessedAt is not null)
            .Select(s => (s.ProcessedAt!.Value - s.UploadedAt).TotalSeconds)
            .ToList();

        return new DashboardKpis
        {
            TotalStatements = statements.Count,
            CompletedCount = statements.Count(s => s.ProcessingStatus == StatementProcessingStatus.Verified),
            InProgressCount = statements.Count(s => s.ProcessingStatus == StatementProcessingStatus.Processing),
            FailedCount = statements.Count(s => s.ProcessingStatus == StatementProcessingStatus.ExtractionFailed),
            TransactionsProcessed = transactions.Count,
            TransactionsNeedingReview = transactions.Count(t => NeedsReview(t)),
            ReconciledCount = currentReconciliations.Count(r => r?.Status == ReconciliationStatus.Reconciled),
            MismatchCount = currentReconciliations.Count(r => r?.Status == ReconciliationStatus.Mismatch),
            PendingReconciliationCount = statements.Count(s => s.ReconciliationResults.Count == 0),
            AverageClassificationConfidence = currentConfidences.Count > 0 ? currentConfidences.Average() : null,
            AverageProcessingTimeSeconds = completedDurations.Count > 0 ? completedDurations.Average() : null
        };
    }

    private async Task<UsersOverviewResponse> BuildUsersOverviewAsync(CancellationToken cancellationToken)
    {
        var users = await dashboardRepository.GetAllUsersAsync(cancellationToken);
        return new UsersOverviewResponse
        {
            TotalUsers = users.Count,
            ActiveUsers = users.Count(u => u.IsActive),
            RoleBreakdown = users.GroupBy(u => u.Role).Select(g => new NamedCountResponse { Name = g.Key.ToString(), Count = g.Count() }).ToList()
        };
    }

    private static bool NeedsReview(Transaction transaction)
    {
        var confidence = transaction.Classifications.FirstOrDefault(c => c.IsCurrent)?.ConfidenceScore;
        var priority = TransactionMapper.ReviewPriority(confidence);
        return priority is "ReviewRecommended" or "ReviewRequired";
    }

    private static List<PipelineStageResponse> BuildPipelineStages(IReadOnlyList<Statement> statements)
    {
        int CountWhere(Func<Statement, bool> predicate) => statements.Count(predicate);
        string StateFor(int count) => count > 0 ? "complete" : "pending";

        var stages = new (string Key, string Label, int Count)[]
        {
            ("upload", "Upload", statements.Count),
            ("text-extraction", "Text Extraction", CountWhere(s => s.StatementExtraction?.ExtractionMethod == ExtractionMethod.DirectPdfText)),
            ("ocr", "OCR", CountWhere(s => s.StatementExtraction is not null && s.StatementExtraction.ExtractionMethod != ExtractionMethod.DirectPdfText)),
            ("transaction-extraction", "Transaction Extraction", CountWhere(s => s.Transactions.Count > 0)),
            ("ai-classification", "AI Classification", CountWhere(s => s.ProcessingStatus is StatementProcessingStatus.ClassificationComplete or StatementProcessingStatus.PendingReview or StatementProcessingStatus.Verified)),
            ("review", "Review", CountWhere(s => s.ProcessingStatus is StatementProcessingStatus.PendingReview or StatementProcessingStatus.Verified)),
            ("reconciliation", "Reconciliation", CountWhere(s => s.ReconciliationResults.Count > 0)),
            ("completed", "Completed", CountWhere(s => s.ProcessingStatus == StatementProcessingStatus.Verified))
        };

        return stages.Select(s => new PipelineStageResponse { Key = s.Key, Label = s.Label, Count = s.Count, State = StateFor(s.Count) }).ToList();
    }

    private static List<NamedCountResponse> BuildProcessingStatusBreakdown(IReadOnlyList<Statement> statements) =>
        Enum.GetValues<StatementProcessingStatus>()
            .Select(status => new NamedCountResponse { Name = status.ToString(), Count = statements.Count(s => s.ProcessingStatus == status) })
            .ToList();

    private static List<DailyTrendPointResponse> BuildProcessingTrend(IReadOnlyList<Statement> statements, int rangeDays)
    {
        var days = Math.Clamp(rangeDays, 1, 365);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = today.AddDays(-(days - 1));

        // Zero-filled, dense series — SQL/LINQ GroupBy naturally skips days with zero rows, but a
        // trend chart's x-axis needs every day represented, not just the ones with activity.
        var series = new List<DailyTrendPointResponse>();
        for (var date = start; date <= today; date = date.AddDays(1))
        {
            var uploadedThatDay = statements.Where(s => DateOnly.FromDateTime(s.UploadedAt) == date).ToList();
            var processedThatDay = statements.Where(s => s.ProcessedAt is not null && DateOnly.FromDateTime(s.ProcessedAt.Value) == date).ToList();

            series.Add(new DailyTrendPointResponse
            {
                Date = date,
                UploadedCount = uploadedThatDay.Count,
                CompletedCount = processedThatDay.Count(s => s.ProcessingStatus != StatementProcessingStatus.ExtractionFailed),
                FailedCount = processedThatDay.Count(s => s.ProcessingStatus == StatementProcessingStatus.ExtractionFailed)
            });
        }

        return series;
    }

    private static List<CategoryBreakdownResponse> BuildCategoryBreakdown(IReadOnlyList<Transaction> transactions) =>
        transactions
            .GroupBy(t => t.Category?.Name ?? "Uncategorized")
            .Select(g => new CategoryBreakdownResponse
            {
                CategoryName = g.Key,
                TransactionCount = g.Count(),
                // Magnitude, not signed sum — a category is virtually always all-debit or
                // all-credit in practice, but summing signed amounts would render as a
                // near-zero or negative slice on a spend-breakdown chart, which reads as "this
                // category has no spend" even when it clearly does.
                TotalAmount = g.Sum(t => Math.Abs(t.Amount ?? 0m))
            })
            .OrderByDescending(c => c.TotalAmount)
            .ToList();

    private static List<NamedCountResponse> BuildConfidenceDistribution(IReadOnlyList<Transaction> transactions)
    {
        var buckets = new[] { "HighConfidence", "ReviewRecommended", "ReviewRequired", "Unclassified" };
        return buckets
            .Select(bucket => new NamedCountResponse
            {
                Name = bucket,
                Count = transactions.Count(t =>
                    (TransactionMapper.ReviewPriority(t.Classifications.FirstOrDefault(c => c.IsCurrent)?.ConfidenceScore) ?? "Unclassified") == bucket)
            })
            .ToList();
    }

    private static List<NamedCountResponse> BuildReconciliationStatusBreakdown(IReadOnlyList<Statement> statements) =>
        statements
            .Select(s => s.ReconciliationResults.OrderByDescending(r => r.CreatedAt).FirstOrDefault())
            .Where(r => r is not null)
            .GroupBy(r => r!.Status)
            .Select(g => new NamedCountResponse { Name = g.Key.ToString(), Count = g.Count() })
            .ToList();

    private static ReviewStatisticsResponse BuildReviewStatistics(IReadOnlyList<Transaction> transactions)
    {
        var flaggedForReview = transactions.Where(NeedsReview).ToList();

        return new ReviewStatisticsResponse
        {
            PendingCount = flaggedForReview.Count(t => t.Corrections.Count == 0),
            CorrectedCount = transactions.Count(t => t.Corrections.Count > 0),
            // No explicit "accepted AI suggestion" action/event is recorded anywhere in this
            // schema — the best honest proxy available is "flagged for review, but nobody
            // corrected it," i.e. a human looked at (or had the chance to look at) a low-
            // confidence suggestion and left it as-is rather than changing it.
            AiAcceptedCount = flaggedForReview.Count(t => t.Corrections.Count == 0)
        };
    }

    private static List<ActivityItemResponse> BuildRecentActivity(IReadOnlyList<Statement> statements, IReadOnlyList<TransactionCorrection> recentCorrections)
    {
        var uploads = statements
            .OrderByDescending(s => s.UploadedAt)
            .Take(RecentActivityCount)
            .Select(s => new ActivityItemResponse { Type = "Upload", Description = $"{s.OriginalFileName} uploaded", Timestamp = s.UploadedAt, StatementId = s.Id });

        var completions = statements
            .Where(s => s.ProcessedAt is not null)
            .OrderByDescending(s => s.ProcessedAt)
            .Take(RecentActivityCount)
            .Select(s => new ActivityItemResponse
            {
                Type = s.ProcessingStatus == StatementProcessingStatus.ExtractionFailed ? "Failed" : "Completed",
                Description = s.ProcessingStatus == StatementProcessingStatus.ExtractionFailed
                    ? $"{s.OriginalFileName} failed to process"
                    : $"{s.OriginalFileName} finished processing",
                Timestamp = s.ProcessedAt!.Value,
                StatementId = s.Id
            });

        var corrections = recentCorrections.Select(c => new ActivityItemResponse
        {
            Type = "Correction",
            Description = $"Corrected {c.FieldName} on a transaction",
            Timestamp = c.CorrectedAt,
            StatementId = c.Transaction?.StatementId
        });

        return uploads.Concat(completions).Concat(corrections)
            .OrderByDescending(a => a.Timestamp)
            .Take(RecentActivityCount)
            .ToList();
    }
}
