namespace FinancialStatementAI.Domain.Entities;

/// <summary>Daily rollup of AIRequest rows per provider/model/request type, so cost/usage
/// dashboards don't need to aggregate the full AIRequest table on every read.</summary>
public class AIUsageMetric : BaseEntity
{
    public DateOnly Date { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string RequestType { get; set; } = string.Empty;

    public int RequestCount { get; set; }
    public long TotalPromptTokens { get; set; }
    public long TotalCompletionTokens { get; set; }
    public long TotalTokens { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
