namespace FinancialStatementAI.Application.DTOs.Transactions;

/// <summary>One audit-trail row: a human-reviewed field correction, original value preserved
/// alongside the corrected one (challenge requirement #9).</summary>
public class TransactionCorrectionResponse
{
    public Guid Id { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string? OriginalValue { get; set; }
    public string CorrectedValue { get; set; } = string.Empty;
    public string? CorrectedByUserName { get; set; }
    public DateTime CorrectedAt { get; set; }
    public string? CorrectionReason { get; set; }
}
