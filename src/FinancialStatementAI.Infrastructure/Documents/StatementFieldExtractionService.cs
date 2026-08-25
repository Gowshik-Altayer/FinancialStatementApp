using System.Globalization;
using System.Text.RegularExpressions;
using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Application.Interfaces;

namespace FinancialStatementAI.Infrastructure.Documents;

/// <summary>Best-effort, label-driven extraction of statement-level fields (requirement #3) from
/// raw text. Looks for common "Label: value" / "Label value" phrasings; a field that isn't found
/// stays null rather than being guessed — see requirement #3's "handle incomplete information
/// gracefully" and requirement #16's hallucination-prevention principle.</summary>
public class StatementFieldExtractionService : IStatementFieldExtractionService
{
    // \d+ (not \d{1,3}) for the leading digit group — it must match a plain 4+-digit number
    // with no thousands separator (e.g. "1000.00"), not just comma-grouped ones ("1,000.00").
    private static readonly Regex AmountAfterLabel = new(@"[$€£]?\s*(?<amount>-?\d+(?:,\d{3})*\.\d{2})", RegexOptions.IgnoreCase);

    public ExtractedStatementFields Extract(string rawText)
    {
        return new ExtractedStatementFields
        {
            AccountHolderName = MatchLabel(rawText, @"Account\s*Holder(?:\s*Name)?"),
            ProviderName = MatchLabel(rawText, @"(?:Bank|Card\s*Provider|Provider)\s*Name"),
            AccountNumberMasked = MatchAccountNumber(rawText),
            OpeningBalance = MatchAmountLabel(rawText, @"Opening\s*Balance"),
            ClosingBalance = MatchAmountLabel(rawText, @"Closing\s*Balance"),
            TotalDebits = MatchAmountLabel(rawText, @"Total\s*Debits?"),
            TotalCredits = MatchAmountLabel(rawText, @"Total\s*Credits?"),
            TotalPayments = MatchAmountLabel(rawText, @"Total\s*Payments?"),
            TotalPurchases = MatchAmountLabel(rawText, @"Total\s*Purchases?"),
            Currency = MatchCurrency(rawText)
        };
    }

    private static string? MatchLabel(string text, string labelPattern)
    {
        var match = Regex.Match(text, $@"{labelPattern}\s*[:\-]?\s*(?<value>[A-Za-z][A-Za-z .'\-]{{2,60}})", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }

    private static string? MatchAccountNumber(string text)
    {
        var match = Regex.Match(text, @"Account\s*(?:Number|No\.?|#)?\s*[:\-]?\s*(?<value>[\dXx*]{4,}[\dXx*\s-]*\d{2,4})", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }

    private static decimal? MatchAmountLabel(string text, string labelPattern)
    {
        var labelMatch = Regex.Match(text, labelPattern, RegexOptions.IgnoreCase);
        if (!labelMatch.Success)
        {
            return null;
        }

        var amountMatch = AmountAfterLabel.Match(text, labelMatch.Index + labelMatch.Length);
        if (!amountMatch.Success || amountMatch.Index - (labelMatch.Index + labelMatch.Length) > 20)
        {
            return null; // amount too far from its label to confidently belong to it
        }

        return decimal.TryParse(amountMatch.Groups["amount"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static string? MatchCurrency(string text)
    {
        if (text.Contains('$')) return "USD";
        if (text.Contains('€')) return "EUR";
        if (text.Contains('£')) return "GBP";

        var codeMatch = Regex.Match(text, @"\b(USD|EUR|GBP|INR|CAD|AUD)\b");
        return codeMatch.Success ? codeMatch.Value.ToUpperInvariant() : null;
    }
}
