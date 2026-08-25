using Azure;
using Azure.AI.Vision.ImageAnalysis;
using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace FinancialStatementAI.Infrastructure.OCR;

/// <summary>Real OCR via Azure AI Vision's Read feature. Requires "Azure:Vision:Endpoint" and
/// "Azure:Vision:ApiKey" (the latter via User Secrets/environment — never appsettings.json).
/// Selected instead of <see cref="MockOcrService"/> by setting "Ocr:Provider" to "Azure".</summary>
public class AzureOcrService : IOcrService
{
    private readonly ImageAnalysisClient _client;

    public AzureOcrService(IOptions<AzureVisionOptions> options)
    {
        var settings = options.Value;
        _client = new ImageAnalysisClient(new Uri(settings.Endpoint), new AzureKeyCredential(settings.ApiKey));
    }

    public async Task<OcrResult> ExtractTextAsync(Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        try
        {
            var imageData = await BinaryData.FromStreamAsync(content, cancellationToken);
            var result = await _client.AnalyzeAsync(imageData, VisualFeatures.Read, cancellationToken: cancellationToken);

            var lines = result.Value.Read?.Blocks.SelectMany(block => block.Lines).Select(line => line.Text) ?? [];
            var rawText = string.Join('\n', lines);

            return OcrResult.Success(rawText, confidenceScore: null);
        }
        catch (RequestFailedException ex)
        {
            return OcrResult.Failure($"Azure Vision OCR request failed: {ex.Message}");
        }
    }
}
