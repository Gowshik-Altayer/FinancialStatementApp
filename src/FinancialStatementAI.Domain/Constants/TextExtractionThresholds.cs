namespace FinancialStatementAI.Domain.Constants;

public static class TextExtractionThresholds
{
    /// <summary>Below this average of non-whitespace characters per page, extracted PDF text is
    /// considered unusable (e.g. a scanned image with no embedded text layer, or a handful of
    /// stray artifacts) and OCR/Vision (Phase 8) is needed instead of direct parsing.</summary>
    public const int MinUsableCharactersPerPage = 20;
}
