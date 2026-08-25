namespace FinancialStatementAI.Application.DTOs.Transactions;

/// <summary>A human reviewer's correction. Phase 12 supports correcting Category only — see
/// docs/ai-processing.md for why (Merchant/Description live-corrections would conflict with
/// reprocess's own field refresh; Amount/date corrections would need to also re-trigger
/// reconciliation, which is out of scope for this phase).</summary>
public class CorrectTransactionRequest
{
    public string CategoryName { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
