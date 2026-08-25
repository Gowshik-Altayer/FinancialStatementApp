namespace FinancialStatementAI.Infrastructure.AI.DocumentIntelligence;

public class AzureDocumentIntelligenceOptions
{
    public const string SectionName = "Azure:DocumentIntelligence";

    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}
