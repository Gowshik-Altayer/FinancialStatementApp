namespace FinancialStatementAI.Application.DTOs.Statements;

public class OcrResult
{
    public bool IsSuccess { get; private init; }
    public string RawText { get; private init; } = string.Empty;
    public decimal? ConfidenceScore { get; private init; }
    public string? ErrorMessage { get; private init; }

    /// <summary>Per-region text/confidence/bounding-box detail, when the provider exposes it —
    /// null for providers that only return plain text (e.g. Azure Vision's Read feature isn't
    /// wired to populate this today). Never required reading: <see cref="RawText"/> and
    /// <see cref="ConfidenceScore"/> alone are still the pipeline's primary signal.</summary>
    public IReadOnlyList<OcrTextBlockResult>? TextBlocks { get; private init; }

    public static OcrResult Success(string rawText, decimal? confidenceScore, IReadOnlyList<OcrTextBlockResult>? textBlocks = null) =>
        new() { IsSuccess = true, RawText = rawText, ConfidenceScore = confidenceScore, TextBlocks = textBlocks };

    public static OcrResult Failure(string errorMessage) => new() { IsSuccess = false, ErrorMessage = errorMessage };
}
