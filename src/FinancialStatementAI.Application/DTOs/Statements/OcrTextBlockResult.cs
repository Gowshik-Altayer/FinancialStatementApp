namespace FinancialStatementAI.Application.DTOs.Statements;

/// <summary>One region of recognized text from an OCR pass — a single detected line/block, its
/// confidence, and its axis-aligned bounding box in source-image pixel coordinates. Optional:
/// only the PaddleOCR-backed IOcrService implementation populates these today (Azure Vision's
/// Read API exposes an equivalent shape but isn't wired to it yet) — see docs/ai-processing.md.</summary>
public class OcrTextBlockResult
{
    public int PageNumber { get; init; }
    public string Text { get; init; } = string.Empty;
    public decimal Confidence { get; init; }
    public int X1 { get; init; }
    public int Y1 { get; init; }
    public int X2 { get; init; }
    public int Y2 { get; init; }
}
