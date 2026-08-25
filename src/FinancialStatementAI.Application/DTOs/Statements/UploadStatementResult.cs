namespace FinancialStatementAI.Application.DTOs.Statements;

public class UploadStatementResult
{
    public bool Succeeded { get; private init; }
    public string? Error { get; private init; }
    public StatementDetailResponse? Statement { get; private init; }

    public static UploadStatementResult Success(StatementDetailResponse statement) => new() { Succeeded = true, Statement = statement };
    public static UploadStatementResult Failure(string error) => new() { Succeeded = false, Error = error };
}
