namespace FinancialStatementAI.Domain.Entities;

/// <summary>One unhandled exception caught by the app-wide global exception handler
/// (Api/GlobalExceptionHandler.cs) — distinct from ProcessingError, which only ever records
/// recoverable failures inside the document-processing pipeline. This table is the catch-all for
/// anything else that goes wrong anywhere in the API (a bad request that slipped past validation,
/// an unexpected null reference, a database timeout on an unrelated endpoint, etc.), so an
/// operator can see what actually broke without needing log-file access.</summary>
public class ExceptionLog : BaseEntity
{
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string ExceptionType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string? RequestPath { get; set; }
    public string? RequestMethod { get; set; }
    public Guid? UserId { get; set; }
    public int StatusCode { get; set; }
}
