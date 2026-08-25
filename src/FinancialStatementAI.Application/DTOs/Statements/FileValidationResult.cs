namespace FinancialStatementAI.Application.DTOs.Statements;

public class FileValidationResult
{
    public bool IsValid { get; private init; }
    public string? ErrorMessage { get; private init; }

    /// <summary>The content type confirmed by inspecting the file's actual bytes (magic numbers),
    /// not just the client-supplied Content-Type header or file extension — see requirement #45
    /// (MIME validation as a security control, not a trust-the-client formality).</summary>
    public string ConfirmedContentType { get; private init; } = string.Empty;

    public static FileValidationResult Success(string confirmedContentType) =>
        new() { IsValid = true, ConfirmedContentType = confirmedContentType };

    public static FileValidationResult Failure(string errorMessage) =>
        new() { IsValid = false, ErrorMessage = errorMessage };
}
