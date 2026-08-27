namespace FinancialStatementAI.Application.DTOs.Transactions;

public class TransactionSummaryResponse
{
    public int TotalCount { get; set; }
    public int HighConfidenceCount { get; set; }
    public int NeedingReviewCount { get; set; }
    public int CorrectedCount { get; set; }
}
