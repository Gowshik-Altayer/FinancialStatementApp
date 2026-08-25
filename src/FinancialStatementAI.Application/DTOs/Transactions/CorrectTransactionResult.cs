namespace FinancialStatementAI.Application.DTOs.Transactions;

public class CorrectTransactionResult
{
    public bool Succeeded { get; private init; }
    public bool NotFound { get; private init; }
    public string? Error { get; private init; }
    public TransactionResponse? Transaction { get; private init; }

    public static CorrectTransactionResult Success(TransactionResponse transaction) => new() { Succeeded = true, Transaction = transaction };
    public static CorrectTransactionResult Failure(string error) => new() { Succeeded = false, Error = error };
    public static CorrectTransactionResult AsNotFound() => new() { Succeeded = false, NotFound = true };
}
