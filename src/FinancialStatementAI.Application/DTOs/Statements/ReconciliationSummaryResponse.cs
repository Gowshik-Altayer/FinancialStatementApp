namespace FinancialStatementAI.Application.DTOs.Statements;

/// <summary>One row of the cross-statement Reconciliation page (requirement 9) — the same shape
/// as ReconciliationResponse (a single statement's current reconciliation result), with the
/// statement identity fields a list view needs added on.</summary>
public class ReconciliationSummaryResponse
{
    public Guid StatementId { get; set; }
    public string StatementFileName { get; set; } = string.Empty;
    public decimal? OpeningBalance { get; set; }
    public decimal? TotalCredits { get; set; }
    public decimal? TotalDebits { get; set; }
    public decimal? ExpectedClosingBalance { get; set; }
    public decimal? StatementClosingBalance { get; set; }
    public decimal? Discrepancy { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ReconciliationSummaryCountsResponse
{
    public int ReconciledCount { get; set; }
    public int MismatchCount { get; set; }
    public int InsufficientInformationCount { get; set; }

    /// <summary>Statements with no reconciliation result at all yet — never reprocessed, or still
    /// mid-pipeline.</summary>
    public int PendingCount { get; set; }

    /// <summary>Sum of |Discrepancy| across every statement currently in Mismatch — "how much
    /// money, in total, is currently unaccounted for."</summary>
    public decimal TotalDiscrepancyAmount { get; set; }
}
