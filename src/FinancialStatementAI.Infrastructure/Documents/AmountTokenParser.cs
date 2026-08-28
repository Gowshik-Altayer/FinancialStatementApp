using System.Globalization;
using System.Text.RegularExpressions;

namespace FinancialStatementAI.Infrastructure.Documents;

/// <summary>Shared amount-token parsing used by both <see cref="TransactionExtractionService"/>
/// (text/table sources) and <see cref="SpreadsheetTransactionExtractionService"/> (.xlsx cells) —
/// pulled out so the two extraction paths can never silently drift apart on how they read the
/// same kinds of amount strings (currency symbols, negative indicators, comma grouping).</summary>
internal static class AmountTokenParser
{
    public static bool TryParse(string text, out decimal absoluteAmount, out bool hasNegativeIndicator, out string? currency)
    {
        currency = text.Contains('$') ? "USD" : text.Contains('€') ? "EUR" : text.Contains('£') ? "GBP" : text.Contains('₹') ? "INR" : null;
        hasNegativeIndicator = text.TrimStart().StartsWith('-') || text.Contains('(') || Regex.IsMatch(text, @"\bDR\b", RegexOptions.IgnoreCase);

        var digitsOnly = Regex.Replace(text, @"[^\d.]", "");
        return decimal.TryParse(digitsOnly, NumberStyles.Number, CultureInfo.InvariantCulture, out absoluteAmount);
    }
}
