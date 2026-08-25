namespace FinancialStatementAI.Application.DTOs.Statements;

/// <summary>Statement-level fields pulled out of the raw extracted text (requirement #3). Every
/// field is nullable and stays null if not confidently found — never guessed or defaulted.</summary>
public class ExtractedStatementFields
{
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
}
