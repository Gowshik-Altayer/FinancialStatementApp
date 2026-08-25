namespace FinancialStatementAI.Application.DTOs.Statements;

public class DocumentIntelligenceResult
{
    public bool IsSuccess { get; private init; }
    public string RawText { get; private init; } = string.Empty;

    /// <summary>Structured key/value fields the service was able to identify (e.g.
    /// "AccountNumber", "StatementDate") — a lighter-weight structure than modeling full table
    /// geometry, since this abstraction isn't on the pipeline's critical path yet (see
    /// docs/ai-processing.md); Phase 9's own parsing does the transaction-row extraction.</summary>
    public IReadOnlyDictionary<string, string> Fields { get; private init; } = new Dictionary<string, string>();

    public decimal? ConfidenceScore { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static DocumentIntelligenceResult Success(string rawText, IReadOnlyDictionary<string, string> fields, decimal? confidenceScore) =>
        new() { IsSuccess = true, RawText = rawText, Fields = fields, ConfidenceScore = confidenceScore };

    public static DocumentIntelligenceResult Failure(string errorMessage) => new() { IsSuccess = false, ErrorMessage = errorMessage };
}
