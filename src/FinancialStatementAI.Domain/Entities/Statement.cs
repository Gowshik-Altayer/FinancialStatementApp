using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Domain.Entities;

public class Statement : BaseEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    // Upload metadata
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DocumentType DocumentType { get; set; } = DocumentType.Unknown;

    // Extracted statement-level information — all nullable: "handle incomplete or
    // unavailable information gracefully" (challenge requirement #3).
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

    public StatementProcessingStatus ProcessingStatus { get; set; } = StatementProcessingStatus.Uploaded;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = [];
    public ICollection<ProcessingJob> ProcessingJobs { get; set; } = [];
    public ICollection<ProcessingError> ProcessingErrors { get; set; } = [];
    public ICollection<ReconciliationResult> ReconciliationResults { get; set; } = [];
}
