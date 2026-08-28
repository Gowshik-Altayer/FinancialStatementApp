namespace FinancialStatementAI.Application.DTOs.Transactions;

public class BulkCorrectTransactionResult
{
    public bool Succeeded { get; private init; }
    public bool NotFound { get; private init; }
    public string? Error { get; private init; }
    public int UpdatedCount { get; private init; }
    public TransactionResponse? Transaction { get; private init; }

    public static BulkCorrectTransactionResult Success(int updatedCount, TransactionResponse transaction) =>
        new() { Succeeded = true, UpdatedCount = updatedCount, Transaction = transaction };
    public static BulkCorrectTransactionResult Failure(string error) => new() { Succeeded = false, Error = error };
    public static BulkCorrectTransactionResult AsNotFound() => new() { Succeeded = false, NotFound = true };
}
