namespace FinancialStatementAI.Application.DTOs.Statements;

/// <summary>One table region a document-layout analysis pass found — its reconstructed structure
/// (HTML, since that's what PP-StructureV3 emits and it's a reasonable, renderable, storable
/// shape for a table), confidence, and axis-aligned bounding box.</summary>
public class OcrTableResult
{
    public int PageNumber { get; init; }
    public string Html { get; init; } = string.Empty;
    public decimal Confidence { get; init; }
    public int X1 { get; init; }
    public int Y1 { get; init; }
    public int X2 { get; init; }
    public int Y2 { get; init; }
}
