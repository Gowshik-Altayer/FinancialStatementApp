namespace FinancialStatementAI.Application.DTOs.Statements;

public class StatementSummaryResponse
{
    public Guid Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string? ProviderName { get; set; }
    public DateOnly? StatementPeriodStart { get; set; }
    public DateOnly? StatementPeriodEnd { get; set; }
    public int TransactionCount { get; set; }
    public decimal? TotalDebits { get; set; }
    public decimal? TotalCredits { get; set; }
    public string ProcessingStatus { get; set; } = string.Empty;
    public string? ReconciliationStatus { get; set; }
    public DateTime UploadedAt { get; set; }
}
