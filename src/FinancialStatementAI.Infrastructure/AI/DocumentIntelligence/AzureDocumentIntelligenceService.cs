using Azure;
using Azure.AI.DocumentIntelligence;
using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace FinancialStatementAI.Infrastructure.AI.DocumentIntelligence;

/// <summary>Real structured extraction via Azure AI Document Intelligence's general "prebuilt-
/// document" model (no bank-statement-specific custom model trained). Requires
/// "Azure:DocumentIntelligence:Endpoint" and "...:ApiKey" (the latter via User Secrets/
/// environment). Selected instead of <see cref="MockDocumentIntelligenceService"/> by setting
/// "DocumentIntelligence:Provider" to "Azure".</summary>
public class AzureDocumentIntelligenceService : IDocumentIntelligenceService
{
    private readonly Azure.AI.DocumentIntelligence.DocumentIntelligenceClient _client;

    public AzureDocumentIntelligenceService(IOptions<AzureDocumentIntelligenceOptions> options)
    {
        var settings = options.Value;
        _client = new Azure.AI.DocumentIntelligence.DocumentIntelligenceClient(
            new Uri(settings.Endpoint), new AzureKeyCredential(settings.ApiKey));
    }

    public async Task<DocumentIntelligenceResult> AnalyzeAsync(Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        try
        {
            var binaryData = await BinaryData.FromStreamAsync(content, cancellationToken);
            var operation = await _client.AnalyzeDocumentAsync(
                WaitUntil.Completed, "prebuilt-document", binaryData, cancellationToken: cancellationToken);

            var result = operation.Value;
            var document = result.Documents.FirstOrDefault();
            var fields = document is null
                ? new Dictionary<string, string>()
                : document.Fields
                    .Where(kvp => kvp.Value.Content is not null)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Content!);

            return DocumentIntelligenceResult.Success(result.Content, fields, confidenceScore: null);
        }
        catch (RequestFailedException ex)
        {
            return DocumentIntelligenceResult.Failure($"Azure Document Intelligence request failed: {ex.Message}");
        }
    }
}
