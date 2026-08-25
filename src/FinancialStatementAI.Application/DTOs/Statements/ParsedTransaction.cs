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
    public decimal? DebitAmount { get; set; }
    public decimal? CreditAmount { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public TransactionType TransactionType { get; set; } = TransactionType.Other;
}
