using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Infrastructure.Documents;

/// <summary>Rule-based (not LLM-based — see the interface doc comment) transaction-line parser.
/// Handles the three statement-line shapes called out in the challenge doc directly:
/// "01/08 AMAZON WEB SERVICES 129.45", "Aug 01 | Amazon Web Services | Debit | $129.45", and
/// "01-Aug AMAZON WEB SERVICES -129.45" — plus reasonable generalizations (currency symbols,
/// thousands separators, parenthesized negatives, explicit CR/DR suffixes, multi-line/wrapped
/// descriptions). Known limitation: only recognizes amounts written with exactly two decimal
/// places (a deliberate scoping choice — see <see cref="TrailingAmountRegex"/> — since a bare
/// integer elsewhere on the line, e.g. a reference number, must not be mistaken for an amount).</summary>
public class TransactionExtractionService : ITransactionExtractionService
{
    private static readonly Regex LeadingDateRegex = new(
        @"^(?<date>\d{1,2}[/-]\d{1,2}(?:[/-]\d{2,4})?|\d{1,2}[-\s][A-Za-z]{3,9}|[A-Za-z]{3,9}\s+\d{1,2})\b",
        RegexOptions.Compiled);

    private static readonly Regex SlashOrDashDate = new(
        @"^(?<a>\d{1,2})[/-](?<b>\d{1,2})(?:[/-](?<y>\d{2,4}))?$", RegexOptions.Compiled);

    private static readonly Regex DayMonthDate = new(
        @"^(?<d>\d{1,2})[-\s](?<mon>[A-Za-z]{3,9})$", RegexOptions.Compiled);

    private static readonly Regex MonthDayDate = new(
        @"^(?<mon>[A-Za-z]{3,9})\s+(?<d>\d{1,2})$", RegexOptions.Compiled);

    // Requires exactly two decimal places so a bare reference number (e.g. "REF 123456") is
    // never mistaken for an amount — see class-level doc comment.
    // \d+ (not \d{1,3}) for the leading digit group — it must match a plain 4+-digit number
    // with no thousands separator (e.g. "1000.00"), not just comma-grouped ones ("1,000.00").
    private static readonly Regex TrailingAmountRegex = new(
        @"[-+]?\(?\s*[$€£]?\s*\d+(?:,\d{3})*\.\d{2}\)?\s*(?:CR|DR)?\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] MonthAbbreviations =
        ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    private static readonly Regex TableRowRegex = new(@"<tr[^>]*>(.*?)</tr>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex TableCellRegex = new(@"<t[dh][^>]*>(.*?)</t[dh]>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex HtmlTagRegex = new(@"<[^>]+>", RegexOptions.Compiled);

    // A cell is "just a date" if the whole trimmed cell matches — no need for anything to follow,
    // unlike LeadingDateRegex's use on a full text line where trailing content is expected.
    private static readonly Regex FullCellDateRegex = new(
        @"^(?<date>\d{1,2}[/-]\d{1,2}(?:[/-]\d{2,4})?|\d{1,2}[-\s][A-Za-z]{3,9}|[A-Za-z]{3,9}\s+\d{1,2})$",
        RegexOptions.Compiled);

    // The amount equivalent of FullCellDateRegex: the whole cell/line is an amount and nothing
    // else. Anchored at both ends deliberately — TrailingAmountRegex only anchors at the end, so
    // it would also match a description line that happens to finish with a number, which is
    // exactly the ambiguity this parser has to avoid when cells arrive without column context.
    private static readonly Regex FullCellAmountRegex = new(
        @"^[-+]?\(?\s*[$€£]?\s*\d+(?:,\d{3})*\.\d{2}\)?\s*(?:CR|DR)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Every transaction row except the last is bounded by the next date-only line; the last row
    // has nothing to bound it, so a scanned statement's trailing footer/disclaimer ("This
    // statement reflects transactions posted between ... Please review promptly and report
    // discrepancies within 30 days.") would otherwise be appended straight onto its description.
    // A word-count threshold is the signal used to detect that boundary instead: every real date,
    // description, reference and amount cell observed in sample data tops out around 5-6 words
    // ("PAYROLL DIRECT DEPOSIT - ACME CORP"), while footer sentences run 9-11. Deliberately a
    // heuristic, not a hard rule — see ExtractFromCellPerLineText.
    private const int ProseWordCountThreshold = 8;

    private static bool LooksLikeProse(string line) =>
        line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= ProseWordCountThreshold;

    public IReadOnlyList<ParsedTransaction> ExtractFromTable(string tableHtml, int referenceYear)
    {
        var transactions = new List<ParsedTransaction>();

        foreach (Match rowMatch in TableRowRegex.Matches(tableHtml))
        {
            var cells = TableCellRegex.Matches(rowMatch.Groups[1].Value)
                .Select(m => WebUtility.HtmlDecode(HtmlTagRegex.Replace(m.Groups[1].Value, "")).Trim())
                .ToList();

            if (cells.Count == 0)
            {
                continue;
            }

            var dateIndex = cells.FindIndex(c => FullCellDateRegex.IsMatch(c));
            if (dateIndex < 0)
            {
                continue; // Header row, or a row with nothing recognizable as a date — skip rather than guess.
            }

            var amountIndex = -1;
            for (var i = cells.Count - 1; i >= 0; i--)
            {
                if (i != dateIndex && TrailingAmountRegex.IsMatch(cells[i]))
                {
                    amountIndex = i;
                    break;
                }
            }

            if (amountIndex < 0)
            {
                continue; // No confidently-recognizable amount cell — don't fabricate a transaction.
            }

            var built = BuildTransaction(cells, dateIndex, amountIndex, referenceYear);
            if (built is not null)
            {
                transactions.Add(built);
            }
        }

        return transactions;
    }

    public IReadOnlyList<ParsedTransaction> ExtractFromCellPerLineText(string rawText, int referenceYear)
    {
        var transactions = new List<ParsedTransaction>();

        var lines = rawText
            .Replace("\f", "\n")
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        // Collect the cells of one row at a time. A date-only line closes the previous row and
        // opens a new one; everything up to the first date-only line is the statement header
        // (bank name, account number, column captions) and is discarded.
        List<string>? currentCells = null;

        foreach (var line in lines)
        {
            if (FullCellDateRegex.IsMatch(line))
            {
                AddIfComplete(transactions, currentCells, referenceYear);
                currentCells = [line];
                continue;
            }

            if (currentCells is null)
            {
                continue; // still in the header block before the first date-only line
            }

            if (LooksLikeProse(line))
            {
                // No further date line is coming to bound this (necessarily last) row, so this is
                // the only available signal that the table has ended. Finalize whatever has been
                // collected and stop accumulating — everything after this is document footer, not
                // transaction data.
                AddIfComplete(transactions, currentCells, referenceYear);
                currentCells = null;
                continue;
            }

            currentCells.Add(line);
        }

        AddIfComplete(transactions, currentCells, referenceYear);

        return transactions;
    }

    /// <summary>Turns one accumulated row of cells into a transaction, or drops it if it lacks a
    /// usable date or amount.</summary>
    private static void AddIfComplete(List<ParsedTransaction> transactions, List<string>? cells, int referenceYear)
    {
        if (cells is null || cells.Count == 0)
        {
            return;
        }

        var dateIndex = cells.FindIndex(c => FullCellDateRegex.IsMatch(c));
        if (dateIndex < 0)
        {
            return;
        }

        // Last amount-shaped cell, matching ExtractFromTable's rule. Scanning from the end matters
        // for layouts that also carry a running balance column: the transaction amount precedes it,
        // but a balance is far more often the trailing value, so the last match is the safer pick
        // only because a row here is bounded by the next date line rather than the page.
        var amountIndex = -1;
        for (var i = cells.Count - 1; i >= 0; i--)
        {
            if (i != dateIndex && FullCellAmountRegex.IsMatch(cells[i]))
            {
                amountIndex = i;
                break;
            }
        }

        if (amountIndex < 0)
        {
            return;
        }

        var built = BuildTransaction(cells, dateIndex, amountIndex, referenceYear);
        if (built is not null)
        {
            transactions.Add(built);
        }
    }

    /// <summary>Shared by the table and cell-per-line parsers: both reduce a row to "these cells,
    /// this one is the date, this one is the amount" and then need identical date/amount parsing,
    /// direction classification and signing.</summary>
    private static ParsedTransaction? BuildTransaction(List<string> cells, int dateIndex, int amountIndex, int referenceYear)
    {
        var date = ParseDateToken(FullCellDateRegex.Match(cells[dateIndex]).Groups["date"].Value, referenceYear);
        if (date is null)
        {
            return null;
        }

        if (!TryParseAmountToken(cells[amountIndex], out var absoluteAmount, out var hasNegativeIndicator, out var currency))
        {
            return null;
        }

        var rawLine = string.Join(" | ", cells);
        var description = string.Join(" ", cells.Where((_, i) => i != dateIndex && i != amountIndex)).Trim();
        var transactionType = ClassifyDirection(hasNegativeIndicator, rawLine);
        var signedAmount = transactionType is TransactionType.Debit or TransactionType.Payment or TransactionType.Purchase or TransactionType.Transfer
            ? -absoluteAmount
            : absoluteAmount;

        return new ParsedTransaction
        {
            RawLine = rawLine,
            TransactionDate = date.Value,
            Description = description,
            Merchant = description,
            Amount = signedAmount,
            DebitAmount = signedAmount < 0 ? absoluteAmount : null,
            CreditAmount = signedAmount >= 0 ? absoluteAmount : null,
            Currency = currency,
            TransactionType = transactionType
        };
    }

    public IReadOnlyList<ParsedTransaction> Extract(string rawText, int referenceYear)
    {
        var transactions = new List<ParsedTransaction>();
        ParsedTransaction? current = null;

        foreach (var rawLine in rawText.Replace("\f", "\n").Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var dateMatch = LeadingDateRegex.Match(line);
            var date = dateMatch.Success ? ParseDateToken(dateMatch.Groups["date"].Value, referenceYear) : null;

            if (date is not null)
            {
                var remainder = line[dateMatch.Length..].Trim().Trim('|', '-', ' ');
                var parsed = TryParseTransactionLine(line, remainder, date.Value);
                if (parsed is not null)
                {
                    transactions.Add(parsed);
                    current = parsed;
                    continue;
                }
            }

            // No recognizable date+amount on this line — treat it as a continuation of the
            // previous transaction's description (wrapped/multi-line descriptions, requirement #5).
            if (current is not null)
            {
                current.Description = $"{current.Description} {line}".Trim();
                current.Merchant = current.Description;
            }
        }

        return transactions;
    }

    private static ParsedTransaction? TryParseTransactionLine(string rawLine, string remainder, DateOnly date)
    {
        var amountMatch = TrailingAmountRegex.Match(remainder);
        if (!amountMatch.Success)
        {
            return null; // no confidently-recognizable amount — don't fabricate a transaction
        }

        var descriptionPart = remainder[..amountMatch.Index].Trim().Trim('|').Trim();
        if (!TryParseAmountToken(amountMatch.Value, out var absoluteAmount, out var hasNegativeIndicator, out var currency))
        {
            return null;
        }

        var description = descriptionPart;
        string? explicitTypeSegment = null;

        // Pipe-delimited format: "Amazon Web Services | Debit | $129.45" (date already stripped).
        if (descriptionPart.Contains('|'))
        {
            var segments = descriptionPart.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length > 0)
            {
                description = segments[0];
            }

            explicitTypeSegment = segments.Skip(1)
                .FirstOrDefault(s => s.Equals("Debit", StringComparison.OrdinalIgnoreCase) || s.Equals("Credit", StringComparison.OrdinalIgnoreCase));
        }

        var transactionType = explicitTypeSegment is not null
            ? (explicitTypeSegment.Equals("Debit", StringComparison.OrdinalIgnoreCase) ? TransactionType.Debit : TransactionType.Credit)
            : ClassifyDirection(hasNegativeIndicator, rawLine);

        var signedAmount = transactionType is TransactionType.Debit or TransactionType.Payment or TransactionType.Purchase or TransactionType.Transfer
            ? -absoluteAmount
            : absoluteAmount;

        return new ParsedTransaction
        {
            RawLine = rawLine,
            TransactionDate = date,
            Description = description,
            Merchant = description,
            Amount = signedAmount,
            DebitAmount = signedAmount < 0 ? absoluteAmount : null,
            CreditAmount = signedAmount >= 0 ? absoluteAmount : null,
            Currency = currency,
            TransactionType = transactionType
        };
    }

    private static bool TryParseAmountToken(string text, out decimal absoluteAmount, out bool hasNegativeIndicator, out string? currency)
    {
        currency = text.Contains('$') ? "USD" : text.Contains('€') ? "EUR" : text.Contains('£') ? "GBP" : null;
        hasNegativeIndicator = text.TrimStart().StartsWith('-') || text.Contains('(') || Regex.IsMatch(text, @"\bDR\b", RegexOptions.IgnoreCase);

        var digitsOnly = Regex.Replace(text, @"[^\d.]", "");
        return decimal.TryParse(digitsOnly, NumberStyles.Number, CultureInfo.InvariantCulture, out absoluteAmount);
    }

    private static TransactionType ClassifyDirection(bool hasNegativeIndicator, string context)
    {
        if (Regex.IsMatch(context, @"\bCR\b|\bcredit\b|\brefund\b", RegexOptions.IgnoreCase))
        {
            return TransactionType.Credit;
        }

        if (Regex.IsMatch(context, @"\btransfer\b", RegexOptions.IgnoreCase))
        {
            return TransactionType.Transfer;
        }

        if (Regex.IsMatch(context, @"\bpayment\b", RegexOptions.IgnoreCase))
        {
            return TransactionType.Payment;
        }

        if (Regex.IsMatch(context, @"\bDR\b|\bdebit\b|\bpurchase\b", RegexOptions.IgnoreCase))
        {
            return TransactionType.Debit;
        }

        // No explicit keyword: fall back to the sign — negative/parenthesized/DR-suffixed
        // amounts are money out (Debit), everything else is money in (Credit).
        return hasNegativeIndicator ? TransactionType.Debit : TransactionType.Credit;
    }

    private static DateOnly? ParseDateToken(string token, int referenceYear)
    {
        token = token.Trim();

        var slashOrDash = SlashOrDashDate.Match(token);
        if (slashOrDash.Success)
        {
            var month = int.Parse(slashOrDash.Groups["a"].Value);
            var day = int.Parse(slashOrDash.Groups["b"].Value);
            var year = slashOrDash.Groups["y"].Success ? NormalizeYear(slashOrDash.Groups["y"].Value) : referenceYear;
            return TryCreateDate(year, month, day);
        }

        var dayMonth = DayMonthDate.Match(token);
        if (dayMonth.Success)
        {
            var month = MonthFromAbbreviation(dayMonth.Groups["mon"].Value);
            return month is null ? null : TryCreateDate(referenceYear, month.Value, int.Parse(dayMonth.Groups["d"].Value));
        }

        var monthDay = MonthDayDate.Match(token);
        if (monthDay.Success)
        {
            var month = MonthFromAbbreviation(monthDay.Groups["mon"].Value);
            return month is null ? null : TryCreateDate(referenceYear, month.Value, int.Parse(monthDay.Groups["d"].Value));
        }

        return null;
    }

    private static int NormalizeYear(string yearText)
    {
        var year = int.Parse(yearText);
        return yearText.Length == 2 ? 2000 + year : year;
    }

    private static int? MonthFromAbbreviation(string text)
    {
        var index = Array.FindIndex(MonthAbbreviations, m => text.StartsWith(m, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? null : index + 1;
    }

    private static DateOnly? TryCreateDate(int year, int month, int day)
    {
        try
        {
            return new DateOnly(year, month, day);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
