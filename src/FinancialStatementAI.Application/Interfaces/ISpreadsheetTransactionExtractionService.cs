using FinancialStatementAI.Application.DTOs.Statements;

namespace FinancialStatementAI.Application.Interfaces;

/// <summary>Reads transactions directly from a structured spreadsheet (.xlsx) — there is no
/// OCR/text-layer step in this path at all, unlike every other <see
/// cref="ITransactionExtractionService"/> source, because a spreadsheet's cells are already
/// unambiguous (requirement #5 — normalize regardless of the original statement format). Column
/// headers are matched fuzzily (e.g. "Transaction Date", "Value Date", and "Date" all map to the
/// same field) so real-world exports with differently-named columns don't each need bespoke
/// handling.</summary>
public interface ISpreadsheetTransactionExtractionService
{
    /// <returns>One ParsedTransaction per data row that has a recognizable amount; a row without
    /// one is skipped rather than guessed (requirement #16). A workbook with no date column at all
    /// leaves TransactionDate null on every row, exactly as any other dateless source does — never
    /// fabricated.</returns>
    IReadOnlyList<ParsedTransaction> Extract(Stream xlsxStream);
}
