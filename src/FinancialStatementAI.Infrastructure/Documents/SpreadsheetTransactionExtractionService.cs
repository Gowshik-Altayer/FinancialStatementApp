using System.Globalization;
using ClosedXML.Excel;
using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Infrastructure.Documents;

/// <summary>ClosedXML-based implementation of <see cref="ISpreadsheetTransactionExtractionService"/>.
/// Reads the first worksheet's first row as headers, fuzzy-maps each header to a logical column
/// (date/description/debit/credit/amount/type/reference), then builds one ParsedTransaction per
/// data row directly from typed cell values — never round-tripping through free text the way the
/// PDF/OCR paths must, since a spreadsheet's cells are already unambiguous.</summary>
public class SpreadsheetTransactionExtractionService : ISpreadsheetTransactionExtractionService
{
    public IReadOnlyList<ParsedTransaction> Extract(Stream xlsxStream)
    {
        using var workbook = new XLWorkbook(xlsxStream);
        var worksheet = workbook.Worksheets.FirstOrDefault();
        if (worksheet is null)
        {
            return [];
        }

        var headerRow = worksheet.RowsUsed().FirstOrDefault();
        if (headerRow is null)
        {
            return [];
        }

        var columns = MapColumns(headerRow);
        if (columns.Amount is null && columns.Debit is null && columns.Credit is null)
        {
            // No column we can read an amount from at all — nothing downstream can be built
            // without inventing a value that isn't there (requirement #16).
            return [];
        }

        var transactions = new List<ParsedTransaction>();
        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            var transaction = TryParseRow(row, columns);
            if (transaction is not null)
            {
                transactions.Add(transaction);
            }
        }

        return transactions;
    }

    private static ColumnMap MapColumns(IXLRow headerRow)
    {
        var map = new ColumnMap();
        foreach (var cell in headerRow.CellsUsed())
        {
            var normalized = Normalize(cell.GetString());
            var column = cell.Address.ColumnNumber;

            if (map.Date is null && (normalized is "date" or "transactiondate" or "valuedate" or "postingdate"))
            {
                map.Date = column;
            }
            else if (map.Description is null && (normalized is "description" or "narration" or "details" or "particulars" or "transactiondetails"))
            {
                map.Description = column;
            }
            else if (map.Debit is null && (normalized is "debit" or "debitamount" or "withdrawal"))
            {
                map.Debit = column;
            }
            else if (map.Credit is null && (normalized is "credit" or "creditamount" or "deposit"))
            {
                map.Credit = column;
            }
            else if (map.Amount is null && normalized.StartsWith("amount", StringComparison.Ordinal))
            {
                map.Amount = column;
                map.AmountHeaderCurrency = DetectSymbolCurrency(cell.GetString());
            }
            else if (map.Type is null && (normalized is "type" or "transactiontype" or "drcr"))
            {
                map.Type = column;
            }
            else if (map.Reference is null && (normalized is "reference" or "referencenumber" or "transactionid" or "refno" or "chequeno"))
            {
                map.Reference = column;
            }
        }

        return map;
    }

    private static ParsedTransaction? TryParseRow(IXLRow row, ColumnMap columns)
    {
        decimal? debitAmount = null;
        decimal? creditAmount = null;
        string? currency = null;

        if (columns.Debit is int debitCol && TryReadAmount(row.Cell(debitCol), out var debitValue, out var debitCurrency) && debitValue > 0)
        {
            debitAmount = debitValue;
            currency ??= debitCurrency;
        }

        if (columns.Credit is int creditCol && TryReadAmount(row.Cell(creditCol), out var creditValue, out var creditCurrency) && creditValue > 0)
        {
            creditAmount = creditValue;
            currency ??= creditCurrency;
        }

        TransactionType transactionType;
        if (debitAmount is null && creditAmount is null)
        {
            // Single "Amount" column: direction comes from an explicit Type cell when present,
            // otherwise from the amount's own sign (mirrors TransactionExtractionService's
            // ClassifyDirection fallback for the same ambiguity).
            if (columns.Amount is not int amountCol || !TryReadAmount(row.Cell(amountCol), out var absoluteAmount, out var amountCurrency))
            {
                return null;
            }

            currency ??= amountCurrency ?? columns.AmountHeaderCurrency;
            var typeText = columns.Type is int typeCol ? row.Cell(typeCol).GetString().Trim() : null;
            var hasNegativeIndicator = row.Cell(amountCol).GetString().TrimStart().StartsWith('-') || row.Cell(amountCol).GetString().Contains('(');

            transactionType = !string.IsNullOrEmpty(typeText)
                ? (typeText.StartsWith('D') || typeText.StartsWith('d') ? TransactionType.Debit : TransactionType.Credit)
                : (hasNegativeIndicator ? TransactionType.Debit : TransactionType.Credit);

            if (transactionType == TransactionType.Debit)
            {
                debitAmount = absoluteAmount;
            }
            else
            {
                creditAmount = absoluteAmount;
            }
        }
        else
        {
            transactionType = debitAmount is not null ? TransactionType.Debit : TransactionType.Credit;
        }

        var signedAmount = transactionType == TransactionType.Debit ? -(debitAmount ?? 0) : (creditAmount ?? 0);

        DateOnly? date = null;
        if (columns.Date is int dateCol)
        {
            var dateCell = row.Cell(dateCol);
            if (dateCell.TryGetValue(out DateTime dateTimeValue))
            {
                date = DateOnly.FromDateTime(dateTimeValue);
            }
            else if (DateTime.TryParse(dateCell.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                date = DateOnly.FromDateTime(parsedDate);
            }
        }

        var description = columns.Description is int descCol ? row.Cell(descCol).GetString().Trim() : string.Empty;
        var reference = columns.Reference is int refCol ? row.Cell(refCol).GetString().Trim() : null;

        return new ParsedTransaction
        {
            RawLine = BuildRawLine(row),
            TransactionDate = date,
            Description = description,
            Merchant = description,
            ReferenceNumber = string.IsNullOrWhiteSpace(reference) ? null : reference,
            DebitAmount = debitAmount,
            CreditAmount = creditAmount,
            Amount = signedAmount,
            Currency = currency,
            TransactionType = transactionType
        };
    }

    private static bool TryReadAmount(IXLCell cell, out decimal absoluteAmount, out string? currency)
    {
        if (cell.TryGetValue(out double numericValue))
        {
            absoluteAmount = Math.Abs((decimal)numericValue);
            currency = DetectSymbolCurrency(cell.GetString());
            return true;
        }

        return AmountTokenParser.TryParse(cell.GetString(), out absoluteAmount, out _, out currency);
    }

    private static string? DetectSymbolCurrency(string text) =>
        text.Contains('$') ? "USD" : text.Contains('€') ? "EUR" : text.Contains('£') ? "GBP" : text.Contains('₹') ? "INR" : null;

    private static string BuildRawLine(IXLRow row) =>
        string.Join(" | ", row.CellsUsed().Select(c => c.GetString().Trim()).Where(s => s.Length > 0));

    private static string Normalize(string text) =>
        new string(text.Where(char.IsLetter).ToArray()).ToLowerInvariant();

    private sealed class ColumnMap
    {
        public int? Date { get; set; }
        public int? Description { get; set; }
        public int? Debit { get; set; }
        public int? Credit { get; set; }
        public int? Amount { get; set; }
        public int? Type { get; set; }
        public int? Reference { get; set; }
        public string? AmountHeaderCurrency { get; set; }
    }
}
