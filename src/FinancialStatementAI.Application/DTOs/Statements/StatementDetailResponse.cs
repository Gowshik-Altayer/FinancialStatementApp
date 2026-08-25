namespace FinancialStatementAI.Application.DTOs.Statements;

public class StatementDetailResponse
{
    public Guid Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string DocumentType { get; set; } = string.Empty;

    public string? AccountHolderName { get; set; }
    public string? ProviderName { get; set; }
    public string? AccountNumberMasked { get; set; }
    public DateOnly? StatementPeriodStart { get; set; }
    public DateOnly? StatementPeriodEnd { get; set; }
    public DateOnly? StatementDate { get; set; }
    public decimal? OpeningBalance { get; set; }
    public decimal? ClosingBalance { get; set; }
    public decimal? TotalDebits { get; set; }
    public decimal? TotalCredits { get; set; }
    public decimal? TotalPayments { get; set; }
    public decimal? TotalPurchases { get; set; }
    public string? Currency { get; set; }

    public string ProcessingStatus { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int TransactionCount { get; set; }
}
