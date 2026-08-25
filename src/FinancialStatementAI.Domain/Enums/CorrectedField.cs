namespace FinancialStatementAI.Domain.Enums;

/// <summary>Which field of a Transaction a TransactionCorrection audit row applies to.</summary>
public enum CorrectedField
{
    TransactionDate,
    PostingDate,
    Description,
    Merchant,
    Amount,
    TransactionType,
    Category
}
