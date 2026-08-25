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
            [".png"] = "image/png"
        };
}
