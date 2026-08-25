using Azure.Storage.Blobs;
using FinancialStatementAI.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace FinancialStatementAI.Infrastructure.Storage;

/// <summary>Production storage. Selected instead of <see cref="LocalFileStorageService"/> via
/// the "FileStorage:Provider" configuration switch in DependencyInjection.AddInfrastructure.</summary>
public class AzureBlobStorageService : IFileStorageService
{
    private readonly BlobContainerClient _containerClient;

    public AzureBlobStorageService(IOptions<AzureBlobStorageOptions> options)
    {
        var settings = options.Value;
        _containerClient = new BlobContainerClient(settings.ConnectionString, settings.ContainerName);
    }

    public async Task<string> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var safeExtension = Path.GetExtension(fileName);
        var blobName = $"{Guid.NewGuid():N}{safeExtension}";

        content.Position = 0;
        await _containerClient.UploadBlobAsync(blobName, content, cancellationToken);

        return blobName;
    }

    public async Task<Stream> OpenReadAsync(string storedFilePath, CancellationToken cancellationToken = default)
    {
        var blobClient = _containerClient.GetBlobClient(storedFilePath);
        var download = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return download.Value.Content;
    }

    public async Task DeleteAsync(string storedFilePath, CancellationToken cancellationToken = default)
    {
        await _containerClient.DeleteBlobIfExistsAsync(storedFilePath, cancellationToken: cancellationToken);
    }
}
