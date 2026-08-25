namespace FinancialStatementAI.Infrastructure.Storage;

public class AzureBlobStorageOptions
{
    public const string SectionName = "FileStorage:Azure";

    public string ConnectionString { get; set; } = string.Empty;
    public string ContainerName { get; set; } = "statements";
}
