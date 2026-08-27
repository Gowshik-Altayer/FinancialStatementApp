namespace FinancialStatementAI.Application.DTOs.Transactions;

/// <summary>Every optional filter the Transactions page (requirement 7: search, date range,
/// category, confidence, review status) can apply — bundled into one object rather than a long
/// parameter list now that it's grown past the original search/categoryId/statementId set.</summary>
public class TransactionSearchFilter
{
    public string? Search { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? StatementId { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }

    /// <summary>Minimum current classification confidence (0..1) — transactions with no
    /// classification at all never match a MinConfidence filter, since there's nothing to compare.</summary>
    public decimal? MinConfidence { get; set; }

    /// <summary>"HighConfidence" | "ReviewRecommended" | "ReviewRequired" — matches the same
    /// bucket TransactionMapper.ReviewPriority computes, translated into a confidence range the
    /// database can filter on directly (EF Core can't translate a call to that C# method itself).</summary>
    public string? ReviewPriority { get; set; }

    /// <summary>true = only transactions a human has corrected; false = only ones nobody has
    /// touched; null = either.</summary>
    public bool? HasBeenCorrected { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; }
}
