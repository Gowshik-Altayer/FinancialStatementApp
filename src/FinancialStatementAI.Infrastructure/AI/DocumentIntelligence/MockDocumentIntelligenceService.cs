using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Application.Interfaces;

namespace FinancialStatementAI.Infrastructure.AI.DocumentIntelligence;

/// <summary>Development/demo stand-in, clearly labeled as simulated output — see MockOcrService
/// for the same rationale. Selected by default; set "DocumentIntelligence:Provider" to "Azure"
/// to use the real implementation instead.</summary>
public class MockDocumentIntelligenceService : IDocumentIntelligenceService
{
    public Task<DocumentIntelligenceResult> AnalyzeAsync(Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var fields = new Dictionary<string, string>
        {
            ["_note"] = "MOCK Document Intelligence output - simulated for local development"
        };

        return Task.FromResult(DocumentIntelligenceResult.Success(rawText: string.Empty, fields, confidenceScore: null));
    }
}
