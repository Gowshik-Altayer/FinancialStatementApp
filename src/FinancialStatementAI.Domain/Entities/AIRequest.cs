namespace FinancialStatementAI.Domain.Entities;

/// <summary>One call to an external AI/OCR/Document-Intelligence provider, for cost tracking
/// and auditability — see challenge requirement #46 (AI cost control).</summary>
public class AIRequest : BaseEntity
{
    public Guid? StatementId { get; set; }
    public Statement? Statement { get; set; }

    public Guid? TransactionId { get; set; }
    public Transaction? Transaction { get; set; }

    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string RequestType { get; set; } = string.Empty;

    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? TotalTokens { get; set; }
    public int? DurationMs { get; set; }

    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
