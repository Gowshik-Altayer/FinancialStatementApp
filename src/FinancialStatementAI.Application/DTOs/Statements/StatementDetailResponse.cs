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

    /// <summary>Null until this statement has gone through text extraction (Phase 7) at least once.</summary>
    public bool? HasUsableText { get; set; }
    public int? ExtractedPageCount { get; set; }
    public string? ExtractionMethod { get; set; }
    public string? ReconciliationStatus { get; set; }

    /// <summary>OCR's own confidence in the text it produced — null for a direct-text or
    /// spreadsheet extraction, which has no OCR confidence concept at all.</summary>
    public decimal? ExtractionConfidence { get; set; }

    /// <summary>True when <see cref="ExtractionConfidence"/> is below
    /// <see cref="FinancialStatementAI.Domain.Constants.OcrQualityThresholds.LowConfidenceMaximum"/> — a poor-quality
    /// scan (requirement #14) that still produced usable text but warrants extra scrutiny before
    /// trusting the extracted transactions.</summary>
    public bool IsLowQualityExtraction { get; set; }
}
