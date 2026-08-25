namespace FinancialStatementAI.Application.DTOs.Statements;

public class VerifyStatementResult
{
    public bool Succeeded { get; private init; }
    public bool NotFound { get; private init; }
    public string? Error { get; private init; }
    public StatementDetailResponse? Statement { get; private init; }

    public static VerifyStatementResult Success(StatementDetailResponse statement) => new() { Succeeded = true, Statement = statement };
    public static VerifyStatementResult Failure(string error) => new() { Succeeded = false, Error = error };
    public static VerifyStatementResult AsNotFound() => new() { Succeeded = false, NotFound = true };
}
