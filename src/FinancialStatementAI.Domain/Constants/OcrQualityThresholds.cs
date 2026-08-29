namespace FinancialStatementAI.Domain.Constants;

/// <summary>Below <see cref="LowConfidenceMaximum"/>, an OCR pass produced text but with low
/// enough confidence that a human should treat the extraction with caution — requirement #14's
/// "poor-quality scans" must be handled, not silently accepted as if they were clean. The
/// statement still processes normally (never blocked on this alone, per requirement #14's "should
/// not fail completely"); it only gets flagged for the reviewer's attention.</summary>
public static class OcrQualityThresholds
{
    public const decimal LowConfidenceMaximum = 0.60m;
}
