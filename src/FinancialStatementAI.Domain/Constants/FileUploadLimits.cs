namespace FinancialStatementAI.Domain.Constants;

public static class FileUploadLimits
{
    public const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20 MB

    public static readonly IReadOnlyDictionary<string, string> AllowedExtensionsToContentType =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".xlsx"] = SpreadsheetContentType
        };

    /// <summary>The OOXML spreadsheet MIME type — pulled into a constant because both the
    /// validator (sniffing) and StatementProcessingService (branching to the spreadsheet
    /// extraction path) need to compare against the exact same string.</summary>
    public const string SpreadsheetContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}
