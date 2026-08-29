namespace FinancialStatementAI.Application.DTOs.Transactions;

/// <summary>A human reviewer's correction (requirement #9 — date, description, merchant, amount,
/// debit/credit type, and category are all correctable). Every field is optional: only the ones
/// actually supplied are applied, each producing its own TransactionCorrection audit row, so a
/// reviewer changing just the category still works exactly as before this request grew the other
/// fields. <see cref="CategoryName"/> stays a name rather than an id to match how the Review page
/// already sends it (a `mat-select` bound to category names).</summary>
public class CorrectTransactionRequest
{
    public string? CategoryName { get; set; }
    public DateOnly? TransactionDate { get; set; }
    public string? Description { get; set; }
    public string? Merchant { get; set; }
    public decimal? Amount { get; set; }

    /// <summary>One of the <see cref="Domain.Enums.TransactionType"/> names (e.g. "Debit",
    /// "Credit", "Refund") — a string here (not the enum) so an unrecognized value can be
    /// rejected with a clear error instead of failing model binding silently.</summary>
    public string? TransactionType { get; set; }

    public string? Reason { get; set; }
}
