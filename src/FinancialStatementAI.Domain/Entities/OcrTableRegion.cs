namespace FinancialStatementAI.Domain.Entities;

/// <summary>One table region reconstructed by document-layout analysis (PP-StructureV3) over a
/// Statement — its structure as HTML, confidence, and axis-aligned bounding box.</summary>
public class OcrTableRegion : BaseEntity
{
    public Guid StatementExtractionId { get; set; }
    public StatementExtraction? StatementExtraction { get; set; }

    public int PageNumber { get; set; }
    public string Html { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public int X1 { get; set; }
    public int Y1 { get; set; }
    public int X2 { get; set; }
    public int Y2 { get; set; }
}
