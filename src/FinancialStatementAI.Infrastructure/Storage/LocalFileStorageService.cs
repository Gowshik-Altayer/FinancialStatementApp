using FinancialStatementAI.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace FinancialStatementAI.Infrastructure.Storage;

/// <summary>Development-only storage: writes under a configured root folder outside the web
/// root. Statement identifiers are generated server-side (never derived from the client-supplied
/// file name) to prevent path traversal — see requirement #45.</summary>
public class LocalFileStorageService(IOptions<LocalFileStorageOptions> options) : IFileStorageService
{
    private readonly string _rootPath = options.Value.RootPath;

    public async Task<string> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_rootPath);

        var safeExtension = Path.GetExtension(fileName);
        var storedFileName = $"{Guid.NewGuid():N}{safeExtension}";
        var fullPath = Path.Combine(_rootPath, storedFileName);

        await using var fileStream = File.Create(fullPath);
        content.Position = 0;
        await content.CopyToAsync(fileStream, cancellationToken);

        return storedFileName;
    }

    public Task<Stream> OpenReadAsync(string storedFilePath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveFullPath(storedFilePath);
        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storedFilePath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveFullPath(storedFilePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    private string ResolveFullPath(string storedFilePath)
    {
        // storedFilePath is always a bare, server-generated file name (see SaveAsync) — reject
        // anything containing path separators so a corrupted/tampered value can't escape the root.
        if (storedFilePath.Contains('/') || storedFilePath.Contains('\\'))
        {
            throw new ArgumentException("Invalid stored file path.", nameof(storedFilePath));
        }

        return Path.Combine(_rootPath, storedFilePath);
    }
}
