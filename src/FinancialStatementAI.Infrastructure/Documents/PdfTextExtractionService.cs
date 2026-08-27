using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Constants;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace FinancialStatementAI.Infrastructure.Documents;

/// <summary>Direct text-layer extraction — no OCR/Vision involved. Deciding whether the result is
/// "usable" is the crux of requirement #2 (only fall back to OCR/Vision when direct extraction
/// genuinely can't produce enough text, e.g. a scanned page with no embedded text layer).</summary>
public class PdfTextExtractionService : IPdfTextExtractionService
{
    public PdfExtractionResult Extract(Stream pdfContent)
    {
        using var document = PdfDocument.Open(pdfContent);

        // Page.Text concatenates every glyph in content-stream drawing order with no regard for
        // line breaks — for some PDFs (confirmed on a real sample statement during testing) it
        // glues entire visual lines together into one string with no separator at all. That
        // silently breaks TransactionExtractionService downstream, which requires one transaction
        // per line: HasUsableText still comes back true (there's plenty of text), so the failure
        // is invisible until you notice the statement reprocessed with zero transactions.
        // ContentOrderTextExtractor reconstructs reading order using glyph positions instead of
        // stream order, inserting real line breaks — still direct extraction, no OCR involved.
        var pageTexts = document.GetPages()
            .Select(page => ContentOrderTextExtractor.GetText(page))
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
