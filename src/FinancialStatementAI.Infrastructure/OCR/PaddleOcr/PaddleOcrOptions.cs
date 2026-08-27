namespace FinancialStatementAI.Infrastructure.OCR.PaddleOcr;

public class PaddleOcrOptions
{
    public const string SectionName = "Ocr:PaddleOcr";

    /// <summary>Base URL of the OCR microservice (ocr-service/), e.g. "http://localhost:8000" for
    /// local dev or "http://ocr-service:8000" inside docker-compose.</summary>
    public string BaseUrl { get; set; } = "http://localhost:8000";

    /// <summary>PaddleOCR's model inference (especially PP-StructureV3's layout analysis) is
    /// slow on CPU — long enough that the framework's own HttpClient default (100s) can be too
    /// short for a large scanned multi-page statement.</summary>
    public int TimeoutSeconds { get; set; } = 180;
}
