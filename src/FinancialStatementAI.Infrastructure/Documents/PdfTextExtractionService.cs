using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Constants;
using UglyToad.PdfPig;

namespace FinancialStatementAI.Infrastructure.Documents;

/// <summary>Direct text-layer extraction — no OCR/Vision involved. Deciding whether the result is
/// "usable" is the crux of requirement #2 (only fall back to OCR/Vision when direct extraction
/// genuinely can't produce enough text, e.g. a scanned page with no embedded text layer).</summary>
public class PdfTextExtractionService : IPdfTextExtractionService
{
    public PdfExtractionResult Extract(Stream pdfContent)
    {
        using var document = PdfDocument.Open(pdfContent);

        var pageTexts = document.GetPages()
            .Select(page => page.Text)
            .ToList();

        var characterCount = pageTexts.Sum(text => text.Count(c => !char.IsWhiteSpace(c)));
        var averageCharactersPerPage = pageTexts.Count == 0 ? 0 : characterCount / (double)pageTexts.Count;

        return new PdfExtractionResult
        {
            PageTexts = pageTexts,
            CharacterCount = characterCount,
            HasUsableText = averageCharactersPerPage >= TextExtractionThresholds.MinUsableCharactersPerPage
        };
    }
}
