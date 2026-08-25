using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Domain.Entities;

/// <summary>Document-level text extraction result for a Statement — the raw source text pulled
/// from the PDF (directly, or via OCR once Phase 8 lands) before Phase 9 parses individual
/// transactions out of it. Kept separate from <see cref="TransactionExtraction"/>, which is
/// per-transaction and only exists once transactions have actually been parsed out of this.</summary>
public class StatementExtraction : BaseEntity
{
    public Guid StatementId { get; set; }
    public Statement? Statement { get; set; }

    public ExtractionMethod ExtractionMethod { get; set; }
    public string RawText { get; set; } = string.Empty;
    public int PageCount { get; set; }
    public int CharacterCount { get; set; }

    /// <summary>Whether the extracted text is sufficient to proceed with direct parsing, or
    /// whether OCR/Vision (Phase 8) is needed instead — see requirement #2's core decision.</summary>
    public bool HasUsableText { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
