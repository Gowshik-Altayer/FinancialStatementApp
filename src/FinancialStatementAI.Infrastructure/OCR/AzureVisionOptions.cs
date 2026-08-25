namespace FinancialStatementAI.Infrastructure.OCR;

public class AzureVisionOptions
{
    public const string SectionName = "Azure:Vision";

    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}
