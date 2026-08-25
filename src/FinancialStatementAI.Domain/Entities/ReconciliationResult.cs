using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Domain.Entities;

/// <summary>Result of one deterministic reconciliation run for a Statement
/// (Opening Balance + Credits - Debits = Expected Closing Balance, compared against the
/// statement's own reported closing balance). Never computed by AI — see requirement #20.
/// A Statement can have several of these (e.g. after a reprocess); the most recent one
/// (by CreatedAt) is the current result.</summary>
public class ReconciliationResult : BaseEntity
{
    public Guid StatementId { get; set; }
    public Statement? Statement { get; set; }

    public decimal? OpeningBalance { get; set; }
    public decimal? TotalCredits { get; set; }
    public decimal? TotalDebits { get; set; }
    public decimal? ExpectedClosingBalance { get; set; }
    public decimal? StatementClosingBalance { get; set; }
    public decimal? Discrepancy { get; set; }

    public ReconciliationStatus Status { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
