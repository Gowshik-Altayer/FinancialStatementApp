using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Application.Interfaces;

namespace FinancialStatementAI.Infrastructure.OCR;

/// <summary>Development/demo stand-in — returns clearly-labeled simulated OCR output instead of
/// calling a real OCR engine, so the OCR branch of the pipeline is exercisable and demoable
/// without an Azure subscription. Selected by default; set "Ocr:Provider" to "Azure" (plus
/// Azure:Vision:Endpoint/ApiKey) to use the real implementation instead.</summary>
public class MockOcrService : IOcrService
{
    public Task<OcrResult> ExtractTextAsync(Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        const string simulatedText =
            "[MOCK OCR OUTPUT - simulated for local development, not a real OCR result]\n" +
            "STATEMENT PERIOD 01/01/2026 - 01/31/2026\n" +
            "01/02 GROCERY STORE PURCHASE 45.67\n" +
            "02/02 GAS STATION FUEL 32.10\n" +
            "05/02 ONLINE PAYMENT THANK YOU -150.00\n" +
            "Opening Balance 1000.00 Closing Balance 771.23";

        return Task.FromResult(OcrResult.Success(simulatedText, confidenceScore: 0.75m));
    }
}
