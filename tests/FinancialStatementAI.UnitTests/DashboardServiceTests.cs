using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Application.Services;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Domain.Enums;
using Moq;

namespace FinancialStatementAI.UnitTests;

public class DashboardServiceTests
{
    private readonly Mock<IDashboardRepository> _repository = new();
    private static readonly Guid UserId = Guid.NewGuid();

    private DashboardService CreateService() => new(_repository.Object);

    private void SetUp(IReadOnlyList<Statement> statements, IReadOnlyList<TransactionCorrection>? corrections = null)
    {
        _repository.Setup(r => r.GetStatementsForDashboardAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(statements);
        _repository.Setup(r => r.GetRecentCorrectionsAsync(It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(corrections ?? []);
    }

    private static Statement MakeStatement(
        StatementProcessingStatus status,
        DateTime uploadedAt,
        DateTime? processedAt = null,
        ExtractionMethod? extractionMethod = null,
        ReconciliationStatus? reconciliationStatus = null,
        params Transaction[] transactions)
    {
        var statement = new Statement
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            OriginalFileName = "statement.pdf",
            ProcessingStatus = status,
            UploadedAt = uploadedAt,
            ProcessedAt = processedAt,
            Transactions = transactions.ToList()
        };

        if (extractionMethod is not null)
        {
            statement.StatementExtraction = new StatementExtraction { StatementId = statement.Id, ExtractionMethod = extractionMethod.Value };
        }

        if (reconciliationStatus is not null)
        {
            statement.ReconciliationResults = [new ReconciliationResult { StatementId = statement.Id, Status = reconciliationStatus.Value, CreatedAt = DateTime.UtcNow }];
        }

        return statement;
    }

    private static Transaction MakeTransaction(decimal amount, string categoryName, decimal? confidence, int correctionCount = 0)
    {
        var category = new Category { Name = categoryName };
        var transaction = new Transaction { Amount = amount, Category = category };

        if (confidence is not null)
        {
            transaction.Classifications = [new TransactionClassification { ConfidenceScore = confidence.Value, IsCurrent = true }];
        }

        transaction.Corrections = Enumerable.Range(0, correctionCount)
            .Select(_ => new TransactionCorrection { FieldName = CorrectedField.Category })
            .ToList();

        return transaction;
    }

    [Fact]
    public async Task Kpis_Count_Statements_By_Their_Current_Processing_Status()
    {
        SetUp([
            MakeStatement(StatementProcessingStatus.Verified, DateTime.UtcNow),
            MakeStatement(StatementProcessingStatus.Processing, DateTime.UtcNow),
            MakeStatement(StatementProcessingStatus.ExtractionFailed, DateTime.UtcNow),
            MakeStatement(StatementProcessingStatus.Uploaded, DateTime.UtcNow)
        ]);

        var result = await CreateService().GetSummaryAsync(UserId, isAdmin: false, rangeDays: 30);

        Assert.Equal(4, result.Kpis.TotalStatements);
        Assert.Equal(1, result.Kpis.CompletedCount);
        Assert.Equal(1, result.Kpis.InProgressCount);
        Assert.Equal(1, result.Kpis.FailedCount);
    }

    [Fact]
    public async Task Average_Processing_Time_Is_Null_When_Nothing_Has_Finished_Yet()
    {
        SetUp([MakeStatement(StatementProcessingStatus.Processing, DateTime.UtcNow)]);

        var result = await CreateService().GetSummaryAsync(UserId, isAdmin: false, rangeDays: 30);

        Assert.Null(result.Kpis.AverageProcessingTimeSeconds);
    }

    [Fact]
    public async Task Average_Processing_Time_Is_Computed_Only_Over_Statements_That_Finished()
    {
        var uploaded = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var processed = uploaded.AddMinutes(10);
        SetUp([
            MakeStatement(StatementProcessingStatus.Verified, uploaded, processed),
            MakeStatement(StatementProcessingStatus.Processing, DateTime.UtcNow) // still running, excluded
        ]);

        var result = await CreateService().GetSummaryAsync(UserId, isAdmin: false, rangeDays: 30);

        Assert.Equal(600, result.Kpis.AverageProcessingTimeSeconds);
    }

    [Fact]
    public async Task Pipeline_Stages_Report_A_Funnel_Not_A_Live_Stage_Snapshot()
    {
        SetUp([
            MakeStatement(StatementProcessingStatus.Verified, DateTime.UtcNow, extractionMethod: ExtractionMethod.Ocr, reconciliationStatus: ReconciliationStatus.Reconciled,
                transactions: [MakeTransaction(10m, "Food", 0.9m)]),
            MakeStatement(StatementProcessingStatus.Uploaded, DateTime.UtcNow)
        ]);

        var result = await CreateService().GetSummaryAsync(UserId, isAdmin: false, rangeDays: 30);

        var stages = result.PipelineStages.ToDictionary(s => s.Key, s => s.Count);
        Assert.Equal(2, stages["upload"]); // every statement passed through Upload
        Assert.Equal(0, stages["text-extraction"]); // the one extraction present was OCR, not direct text
        Assert.Equal(1, stages["ocr"]);
        Assert.Equal(1, stages["transaction-extraction"]);
        Assert.Equal(1, stages["completed"]);
    }

    [Fact]
    public async Task Category_Breakdown_Uses_Spend_Magnitude_Not_Signed_Sum()
    {
        SetUp([
            MakeStatement(StatementProcessingStatus.Verified, DateTime.UtcNow,
                transactions: [MakeTransaction(-50m, "Groceries", 0.9m), MakeTransaction(-25m, "Groceries", 0.9m)])
        ]);

        var result = await CreateService().GetSummaryAsync(UserId, isAdmin: false, rangeDays: 30);

        var groceries = Assert.Single(result.TransactionsByCategory);
        Assert.Equal("Groceries", groceries.CategoryName);
        Assert.Equal(75m, groceries.TotalAmount); // magnitude of two debits, not a signed sum
        Assert.Equal(2, groceries.TransactionCount);
    }

    [Fact]
    public async Task Confidence_Distribution_Buckets_Match_The_Review_Queues_Own_Thresholds()
    {
        SetUp([
            MakeStatement(StatementProcessingStatus.Verified, DateTime.UtcNow,
                transactions: [
                    MakeTransaction(1m, "A", 0.95m), // HighConfidence
                    MakeTransaction(1m, "A", 0.70m), // ReviewRecommended
                    MakeTransaction(1m, "A", 0.30m), // ReviewRequired
                    MakeTransaction(1m, "A", null)   // Unclassified
                ])
        ]);

        var result = await CreateService().GetSummaryAsync(UserId, isAdmin: false, rangeDays: 30);

        var buckets = result.ConfidenceDistribution.ToDictionary(b => b.Name, b => b.Count);
        Assert.Equal(1, buckets["HighConfidence"]);
        Assert.Equal(1, buckets["ReviewRecommended"]);
        Assert.Equal(1, buckets["ReviewRequired"]);
        Assert.Equal(1, buckets["Unclassified"]);
    }

    [Fact]
    public async Task Review_Statistics_Treat_An_Uncorrected_Flagged_Transaction_As_Implicitly_Accepted()
    {
        SetUp([
            MakeStatement(StatementProcessingStatus.PendingReview, DateTime.UtcNow,
                transactions: [
                    MakeTransaction(1m, "A", 0.30m, correctionCount: 0), // flagged, left as-is => implicitly accepted
                    MakeTransaction(1m, "A", 0.30m, correctionCount: 1)  // flagged, corrected
                ])
        ]);

        var result = await CreateService().GetSummaryAsync(UserId, isAdmin: false, rangeDays: 30);

        Assert.Equal(1, result.ReviewStatistics.PendingCount);
        Assert.Equal(1, result.ReviewStatistics.AiAcceptedCount);
        Assert.Equal(1, result.ReviewStatistics.CorrectedCount);
    }

    [Fact]
    public async Task PendingReconciliationCount_Only_Counts_Statements_With_No_Reconciliation_Result_At_All()
    {
        SetUp([
            MakeStatement(StatementProcessingStatus.Verified, DateTime.UtcNow, reconciliationStatus: ReconciliationStatus.Reconciled),
            MakeStatement(StatementProcessingStatus.PendingReview, DateTime.UtcNow) // never reconciled
        ]);

        var result = await CreateService().GetSummaryAsync(UserId, isAdmin: false, rangeDays: 30);

        Assert.Equal(1, result.Kpis.PendingReconciliationCount);
        Assert.Equal(1, result.Kpis.ReconciledCount);
    }
}
