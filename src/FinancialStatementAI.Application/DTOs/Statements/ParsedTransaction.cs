using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Application.DTOs.Statements;

/// <summary>A single normalized transaction pulled out of a statement's raw extracted text —
/// intermediate representation between text and the Domain <c>Transaction</c> entity. Only ever
/// carries values actually present in <see cref="RawLine"/>; never invents a value that isn't
/// there (see requirement #16 — hallucination prevention applies to rule-based parsing too, not
/// just LLM output).</summary>
public class ParsedTransaction
{
    public string RawLine { get; set; } = string.Empty;
    public DateOnly? TransactionDate { get; set; }
    public DateOnly? PostingDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Merchant { get; set; }
    public string? ReferenceNumber { get; set; }
    /// <summary>Which page of the source document this transaction was read from, where the
    /// source format has a meaningful notion of pages (requirement #4 — "page/source location,
    /// where available"). 1-based; null when the source has no page concept (e.g. a spreadsheet
    /// row) or the extraction path doesn't track it.</summary>
    public string? PageSourceLocation { get; set; }
    public decimal? DebitAmount { get; set; }
    public decimal? CreditAmount { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public TransactionType TransactionType { get; set; } = TransactionType.Other;

    /// <summary>How confident the EXTRACTION step is that this row was parsed correctly —
    /// requirement #8 lists this as a confidence type distinct from classification confidence.
    /// Assigned by the caller based on which parsing strategy produced the row (direct line match,
    /// reconstructed table, OCR cell-per-line heuristics, or a typed spreadsheet cell) rather than
    /// per-row, since the strategy actually used is what determines how much inference was
    /// involved. Defaults to 1.0 (fully certain) for any path that doesn't override it.</summary>
    public decimal Confidence { get; set; } = 1.0m;
}
