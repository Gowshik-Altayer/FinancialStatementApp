namespace FinancialStatementAI.Domain.Entities;

/// <summary>One region of recognized text from an OCR pass over a Statement — a single detected
/// line/block, its confidence, and its axis-aligned bounding box in source-image pixel
/// coordinates (see OcrResult.TextBlocks, populated by the PaddleOCR-backed IOcrService).</summary>
public class OcrTextBlock : BaseEntity
{
    public Guid StatementExtractionId { get; set; }
    public StatementExtraction? StatementExtraction { get; set; }

    public int PageNumber { get; set; }
    public string Text { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public int X1 { get; set; }
    public int Y1 { get; set; }
    public int X2 { get; set; }
    public int Y2 { get; set; }
}
