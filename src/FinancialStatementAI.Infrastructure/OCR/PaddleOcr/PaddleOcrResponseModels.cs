namespace FinancialStatementAI.Infrastructure.OCR.PaddleOcr;

// Internal wire-format DTOs matching ocr-service/app/models.py's Pydantic response shapes —
// deliberately kept private to this namespace (never referenced outside PaddleOcrService /
// PaddleDocumentStructureService) so the Python service's JSON contract never leaks into the
// Application-layer OcrResult/DocumentIntelligenceResult types the rest of the app depends on.

internal class PaddleOcrResponse
{
    public bool Success { get; set; }
    public string RawText { get; set; } = string.Empty;
    public decimal? Confidence { get; set; }
    public List<PaddleOcrPage> Pages { get; set; } = [];
    public string? ErrorMessage { get; set; }
}

internal class PaddleOcrPage
{
    public int PageNumber { get; set; }
    public List<PaddleTextBlock> TextBlocks { get; set; } = [];
}

internal class PaddleTextBlock
{
    public string Text { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public int X1 { get; set; }
    public int Y1 { get; set; }
    public int X2 { get; set; }
    public int Y2 { get; set; }
}

internal class PaddleStructureResponse
{
    public bool Success { get; set; }
    public List<PaddleTableResult> Tables { get; set; } = [];
    public string? ErrorMessage { get; set; }
}

internal class PaddleTableResult
{
    public int PageNumber { get; set; }
    public string Html { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public int X1 { get; set; }
    public int Y1 { get; set; }
    public int X2 { get; set; }
    public int Y2 { get; set; }
}
