namespace FinancialStatementAI.Application.DTOs.Statements;

public class PdfExtractionResult
{
    public IReadOnlyList<string> PageTexts { get; init; } = [];
    public int PageCount => PageTexts.Count;
    public string RawText => string.Join("\f", PageTexts); // form-feed between pages, mirroring the PDF page-break convention

    public int CharacterCount { get; init; }
    public bool HasUsableText { get; init; }
}
