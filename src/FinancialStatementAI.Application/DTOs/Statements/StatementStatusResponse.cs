namespace FinancialStatementAI.Application.DTOs.Statements;

public class StatementStatusResponse
{
    public Guid Id { get; set; }
    public string ProcessingStatus { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
