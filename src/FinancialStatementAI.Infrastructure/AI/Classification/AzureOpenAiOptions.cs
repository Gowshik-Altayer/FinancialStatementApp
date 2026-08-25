namespace FinancialStatementAI.Infrastructure.AI.Classification;

public class AzureOpenAiOptions
{
    public const string SectionName = "Azure:OpenAI";

    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
}
