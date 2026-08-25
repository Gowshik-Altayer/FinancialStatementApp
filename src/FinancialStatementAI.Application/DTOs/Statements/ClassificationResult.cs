namespace FinancialStatementAI.Application.DTOs.Statements;

public class ClassificationResult
{
    public bool IsSuccess { get; private init; }
    public string? CategoryName { get; private init; }
    public decimal Confidence { get; private init; }
    public string? Reason { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static ClassificationResult Success(string categoryName, decimal confidence, string reason) =>
        new() { IsSuccess = true, CategoryName = categoryName, Confidence = confidence, Reason = reason };

    public static ClassificationResult Failure(string errorMessage) => new() { IsSuccess = false, ErrorMessage = errorMessage };
}
