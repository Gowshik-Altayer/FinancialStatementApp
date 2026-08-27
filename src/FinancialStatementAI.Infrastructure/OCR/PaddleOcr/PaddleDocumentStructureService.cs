using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Application.Interfaces;

namespace FinancialStatementAI.Infrastructure.OCR.PaddleOcr;

/// <summary>Document layout / table structure analysis via the PaddleOCR microservice's
/// PP-StructureV3 pipeline. Distinct from PaddleOcrService (plain text + confidence, PP-OCRv6):
/// this reconstructs table regions into HTML, for statements where the transaction table's
/// structure itself matters (see docs/ai-processing.md). Selected via
/// "DocumentIntelligence:Provider" = "PaddleOcr".</summary>
public class PaddleDocumentStructureService(HttpClient httpClient) : IDocumentIntelligenceService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<DocumentIntelligenceResult> AnalyzeAsync(Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            using var fileContent = new StreamContent(content);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
            form.Add(fileContent, "file", "statement");

            using var httpResponse = await httpClient.PostAsync("structure", form, cancellationToken);
            httpResponse.EnsureSuccessStatusCode();

            var body = await httpResponse.Content.ReadFromJsonAsync<PaddleStructureResponse>(JsonOptions, cancellationToken);
            if (body is null)
            {
                return DocumentIntelligenceResult.Failure("PaddleOCR structure service returned an empty response.");
            }

            if (!body.Success)
            {
                return DocumentIntelligenceResult.Failure(body.ErrorMessage ?? "PaddleOCR structure service reported failure without a message.");
            }

            var tables = body.Tables
                .Select(t => new OcrTableResult
                {
                    PageNumber = t.PageNumber,
                    Html = t.Html,
                    Confidence = t.Confidence,
                    X1 = t.X1,
                    Y1 = t.Y1,
                    X2 = t.X2,
                    Y2 = t.Y2
                })
                .ToList();

            var overallConfidence = tables.Count > 0 ? tables.Average(t => t.Confidence) : (decimal?)null;

            // No flat key/value Fields here — PP-StructureV3 does layout/table reconstruction,
            // not the field-level extraction this DTO's Fields property models (see its own doc
            // comment); Phase 9's own rule-based parsing remains what turns raw text into fields.
            return DocumentIntelligenceResult.Success(rawText: string.Empty, fields: new Dictionary<string, string>(), overallConfidence, tables);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            return DocumentIntelligenceResult.Failure($"PaddleOCR structure service request failed: {ex.Message}");
        }
    }
}
