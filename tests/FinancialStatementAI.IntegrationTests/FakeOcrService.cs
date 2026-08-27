using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Application.Interfaces;

namespace FinancialStatementAI.IntegrationTests;

/// <summary>Stands in for PaddleOcrService in integration tests, which run with no PaddleOCR
/// microservice available — mirrors this codebase's established pattern of never exercising a
/// real external-service-backed implementation in tests (see
/// GlobalExceptionHandlerWebApplicationFactory/HangfireWebApplicationFactory for the same
/// approach). Always "succeeds" with simulated usable text, the same behavior the now-removed
/// MockOcrService provided, so the OCR-fallback path in StatementProcessingService still has
/// something deterministic to exercise end-to-end.</summary>
public class FakeOcrService : IOcrService
{
    public Task<OcrResult> ExtractTextAsync(Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        const string simulatedText = "01/08 SIMULATED OCR TRANSACTION 42.00 payment thank you";
        var textBlocks = new List<OcrTextBlockResult>
        {
            new()
            {
                PageNumber = 1,
                Text = simulatedText,
                Confidence = 0.97m,
                X1 = 10,
                Y1 = 10,
                X2 = 400,
                Y2 = 30
            }
        };

        return Task.FromResult(OcrResult.Success(simulatedText, confidenceScore: 0.97m, textBlocks: textBlocks));
    }
}
