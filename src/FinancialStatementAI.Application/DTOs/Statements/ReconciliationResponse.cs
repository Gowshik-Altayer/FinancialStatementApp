namespace FinancialStatementAI.Application.DTOs.Statements;

public class ReconciliationResponse
{
    public Guid Id { get; set; }
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
