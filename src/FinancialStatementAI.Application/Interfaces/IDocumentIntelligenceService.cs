using FinancialStatementAI.Application.DTOs.Statements;

namespace FinancialStatementAI.Application.Interfaces;

/// <summary>Extracts structured information (fields, tables, layout) rather than plain text —
/// see requirement #14. Not currently on the pipeline's critical path (OCR + Phase 9's own
/// parsing handle transaction extraction); this abstraction exists so a future phase can swap in
/// structured field extraction without the business layer ever depending on the Azure SDK
/// directly.</summary>
public interface IDocumentIntelligenceService
{
    Task<DocumentIntelligenceResult> AnalyzeAsync(Stream content, string contentType, CancellationToken cancellationToken = default);
}
