using FinancialStatementAI.Application.DTOs.Statements;

namespace FinancialStatementAI.Application.Interfaces;

/// <summary>Parses individual transaction rows out of a statement's raw extracted text and
/// normalizes them into a consistent representation, regardless of the source statement's
/// original layout (requirement #5). Deterministic and rule-based, not LLM-driven — extracting
/// exact dates/amounts is a place where hallucination risk must be zero (see requirement #16),
/// which an LLM cannot guarantee the way parsing the literal source text can.</summary>
public interface ITransactionExtractionService
{
    /// <summary>Most statement line dates omit the year (e.g. "01/08"); <paramref
    /// name="referenceYear"/> supplies it — pass the statement period's year when known
    /// (see <see cref="IStatementFieldExtractionService"/>), falling back to the current year.</summary>
    IReadOnlyList<ParsedTransaction> Extract(string rawText, int referenceYear);
}
