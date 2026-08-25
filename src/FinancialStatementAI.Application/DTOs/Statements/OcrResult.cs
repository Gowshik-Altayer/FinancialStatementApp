namespace FinancialStatementAI.Application.DTOs.Statements;

public class OcrResult
{
    public bool IsSuccess { get; private init; }
    public string RawText { get; private init; } = string.Empty;
    public decimal? ConfidenceScore { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static OcrResult Success(string rawText, decimal? confidenceScore) =>
        new() { IsSuccess = true, RawText = rawText, ConfidenceScore = confidenceScore };

    public static OcrResult Failure(string errorMessage) => new() { IsSuccess = false, ErrorMessage = errorMessage };
}
