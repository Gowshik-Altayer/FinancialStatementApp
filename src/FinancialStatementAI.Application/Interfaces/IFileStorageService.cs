namespace FinancialStatementAI.Application.Interfaces;

public interface IFileStorageService
{
    /// <summary>Saves the content and returns a storage-provider-specific key/path that
    /// <see cref="OpenReadAsync"/> and <see cref="DeleteAsync"/> can use to locate it again.</summary>
    Task<string> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string storedFilePath, CancellationToken cancellationToken = default);

    Task DeleteAsync(string storedFilePath, CancellationToken cancellationToken = default);
}
