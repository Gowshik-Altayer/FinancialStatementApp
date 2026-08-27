using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Application.Interfaces;

namespace FinancialStatementAI.Infrastructure.OCR.PaddleOcr;

/// <summary>Real OCR via the PaddleOCR microservice (ocr-service/), running PP-OCRv6. This is the
/// default IOcrService — see docs/ai-processing.md for why PaddleOCR was chosen over Tesseract
/// and Surya, and why OCR has to run as a separate Python service rather than in-process (no
/// viable native .NET port for PaddleOCR/PaddlePaddle). Selected unless "Ocr:Provider" is set to
/// "Azure".</summary>
public class PaddleOcrService(HttpClient httpClient) : IOcrService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<OcrResult> ExtractTextAsync(Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            using var fileContent = new StreamContent(content);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
            form.Add(fileContent, "file", "statement");

            using var httpResponse = await httpClient.PostAsync("ocr", form, cancellationToken);
            httpResponse.EnsureSuccessStatusCode();

            var body = await httpResponse.Content.ReadFromJsonAsync<PaddleOcrResponse>(JsonOptions, cancellationToken);
            if (body is null)
            {
                return OcrResult.Failure("PaddleOCR service returned an empty response.");
            }

            if (!body.Success)
            {
                return OcrResult.Failure(body.ErrorMessage ?? "PaddleOCR service reported failure without a message.");
            }

            var textBlocks = body.Pages
                .SelectMany(page => page.TextBlocks.Select(block => new OcrTextBlockResult
                {
                    PageNumber = page.PageNumber,
                    Text = block.Text,
                    Confidence = block.Confidence,
                    X1 = block.X1,
                    Y1 = block.Y1,
                    X2 = block.X2,
                    Y2 = block.Y2
                }))
                .ToList();

            return OcrResult.Success(body.RawText, body.Confidence, textBlocks);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            // Never lets a transient OCR-service outage crash the pipeline — StatementProcessingService
            // treats a failed OCR attempt the same as "no usable text found" and marks the statement
            // ExtractionFailed, which is the honest outcome here (requirement #16: never fabricate).
            return OcrResult.Failure($"PaddleOCR service request failed: {ex.Message}");
        }
    }
}
