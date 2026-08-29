using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Application.DTOs.Transactions;

/// <summary>The subset of a Transaction's fields a single correction call actually changes —
/// every property is optional, and the repository only touches the ones that are set. Kept
/// separate from <see cref="CorrectTransactionRequest"/> because the request carries a category
/// NAME and a raw type STRING (what the API receives), while this carries the already-resolved
/// category id and parsed enum (what the repository needs to write).</summary>
public class TransactionFieldUpdates
{
    public Guid? CategoryId { get; set; }
    public DateOnly? TransactionDate { get; set; }
    public string? Description { get; set; }
    public string? Merchant { get; set; }

    /// <summary>The new signed amount (negative for money out) — the single source of truth for
    /// DebitAmount/CreditAmount, mirroring how every extraction path already derives them from
    /// the signed Amount rather than from TransactionType.</summary>
    public decimal? Amount { get; set; }

    public TransactionType? TransactionType { get; set; }
}
