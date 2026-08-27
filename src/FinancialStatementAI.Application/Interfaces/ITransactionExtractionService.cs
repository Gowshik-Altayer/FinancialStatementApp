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

    /// <summary>Parses transaction rows out of a reconstructed HTML table (e.g. PP-StructureV3's
    /// table-structure output) rather than line-based text. Needed specifically for OCR'd tabular
    /// statements: plain-text OCR output typically places each table cell on its own line (one
    /// date, then one description, then one amount, ...), which <see cref="Extract"/> can never
    /// reassemble into a row since it requires a date and an amount on the same line. Each
    /// &lt;tr&gt; is treated as one candidate transaction: the first cell matching a recognizable
    /// date becomes the date, the last cell matching a recognizable amount becomes the amount,
    /// everything else becomes the description — a row missing either is skipped, never
    /// guessed (requirement #16).</summary>
    IReadOnlyList<ParsedTransaction> ExtractFromTable(string tableHtml, int referenceYear);
}
