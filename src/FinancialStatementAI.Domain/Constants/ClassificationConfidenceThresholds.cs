namespace FinancialStatementAI.Domain.Constants;

/// <summary>Mirrors the challenge's own example thresholds exactly (requirement #18).</summary>
public static class ClassificationConfidenceThresholds
{
    public const decimal HighConfidenceMinimum = 0.80m;
    public const decimal ReviewRecommendedMinimum = 0.60m;
}
