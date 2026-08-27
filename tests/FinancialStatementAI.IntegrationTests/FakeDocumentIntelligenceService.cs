using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Application.Interfaces;

namespace FinancialStatementAI.IntegrationTests;

/// <summary>Stands in for PaddleDocumentStructureService in integration tests, for the same
/// reason FakeOcrService stands in for PaddleOcrService: no ocr-service/ microservice is
/// available in these tests, and IDocumentIntelligenceService's PaddleOcr provider is now the
/// default (see DependencyInjection.cs). Returns no tables — StatementProcessingService's
/// ExtractTransactions then falls back to its line-based parser, exactly as it did before table
/// extraction existed, which is what every existing OCR-path test already expects.</summary>
public class FakeDocumentIntelligenceService : IDocumentIntelligenceService
{
    public Task<DocumentIntelligenceResult> AnalyzeAsync(Stream content, string contentType, CancellationToken cancellationToken = default) =>
        Task.FromResult(DocumentIntelligenceResult.Success(rawText: string.Empty, fields: new Dictionary<string, string>(), confidenceScore: null));
}
