namespace FinancialStatementAI.Domain.Enums;

/// <summary>Which stage of the hybrid classification pipeline produced a TransactionClassification.</summary>
public enum ClassificationMethod
{
    Rule,
    MerchantMapping,
    PreviousCorrection,
    Llm
}
