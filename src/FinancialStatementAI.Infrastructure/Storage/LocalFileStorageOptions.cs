namespace FinancialStatementAI.Infrastructure.Storage;

public class LocalFileStorageOptions
{
    public const string SectionName = "FileStorage:Local";

    public string RootPath { get; set; } = "App_Data/uploads";
}
