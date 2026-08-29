using System.Globalization;
using System.Text.RegularExpressions;
using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Enums;

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

    // A full calendar date always carries its own year (unlike transaction-line dates, which
    // routinely omit it) — MM/DD/YYYY, "March 31, 2026", or "31 March 2026".
    private static readonly Regex FullDateToken = new(
        @"\d{1,2}[/-]\d{1,2}[/-]\d{2,4}|[A-Za-z]{3,9}\.?\s+\d{1,2}(?:st|nd|rd|th)?,?\s+\d{4}|\d{1,2}(?:st|nd|rd|th)?\s+[A-Za-z]{3,9}\.?,?\s+\d{4}",
        RegexOptions.Compiled);

    private static readonly Regex CreditCardIndicators = new(
        @"\bcredit\s*card\b|\bminimum\s*payment\b|\bpayment\s*due\s*date\b|\btotal\s*purchases\b|\bcredit\s*limit\b|\bavailable\s*credit\b",
        RegexOptions.IgnoreCase);

    private static readonly Regex BankStatementIndicators = new(
        @"\bchecking\s*account\b|\bsavings\s*account\b|\brouting\s*number\b|\bbank\s*statement\b",
        RegexOptions.IgnoreCase);

    public ExtractedStatementFields Extract(string rawText)
    {
        var (periodStart, periodEnd) = MatchStatementPeriod(rawText);

        return new ExtractedStatementFields
        {
            AccountHolderName = MatchLabel(rawText, @"Account\s*Holder(?:\s*Name)?"),
            ProviderName = MatchLabel(rawText, @"(?:Bank|Card\s*Provider|Provider)\s*Name"),
            AccountNumberMasked = MatchAccountNumber(rawText),
            StatementPeriodStart = periodStart,
            StatementPeriodEnd = periodEnd,
            StatementDate = MatchDateLabel(rawText, @"Statement\s*Date"),
            OpeningBalance = MatchAmountLabel(rawText, @"Opening\s*Balance"),
            ClosingBalance = MatchAmountLabel(rawText, @"Closing\s*Balance"),
            TotalDebits = MatchAmountLabel(rawText, @"Total\s*Debits?"),
            TotalCredits = MatchAmountLabel(rawText, @"Total\s*Credits?"),
            TotalPayments = MatchAmountLabel(rawText, @"Total\s*Payments?"),
            TotalPurchases = MatchAmountLabel(rawText, @"Total\s*Purchases?"),
            Currency = MatchCurrency(rawText),
            DocumentType = ClassifyDocumentType(rawText)
        };
    }

    /// <summary>Looks for "Statement Period" followed by the first two full dates within a
    /// nearby window — treated as start then end. Populated only when exactly two dates are found
    /// close enough to the label to confidently belong to it; a single date or none leaves both
    /// null rather than guessing which end of the range it represents.</summary>
    private static (DateOnly? Start, DateOnly? End) MatchStatementPeriod(string text)
    {
        var labelMatch = Regex.Match(text, @"Statement\s*Period", RegexOptions.IgnoreCase);
        if (!labelMatch.Success)
        {
            return (null, null);
        }

        var window = text.Substring(labelMatch.Index + labelMatch.Length,
            Math.Min(60, text.Length - (labelMatch.Index + labelMatch.Length)));
        var dateMatches = FullDateToken.Matches(window);
        if (dateMatches.Count < 2)
        {
            return (null, null);
        }

        var start = TryParseFullDate(dateMatches[0].Value);
        var end = TryParseFullDate(dateMatches[1].Value);
        return (start, end);
    }

    private static DateOnly? MatchDateLabel(string text, string labelPattern)
    {
        var labelMatch = Regex.Match(text, labelPattern, RegexOptions.IgnoreCase);
        if (!labelMatch.Success)
        {
            return null;
        }

        var dateMatch = FullDateToken.Match(text, labelMatch.Index + labelMatch.Length);
        if (!dateMatch.Success || dateMatch.Index - (labelMatch.Index + labelMatch.Length) > 20)
        {
            return null; // date too far from its label to confidently belong to it
        }

        return TryParseFullDate(dateMatch.Value);
    }

    private static DateOnly? TryParseFullDate(string token) =>
        DateTime.TryParse(token, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? DateOnly.FromDateTime(parsed)
            : null;

    /// <summary>Distinguishes a bank statement from a credit card statement using vocabulary that
    /// only realistically appears in one or the other (requirement #1) — never guessed when
    /// neither vocabulary appears, since a statement can genuinely be ambiguous (e.g. combined
    /// checking + credit card summaries).</summary>
    private static DocumentType? ClassifyDocumentType(string text)
    {
        var looksLikeCreditCard = CreditCardIndicators.IsMatch(text);
        var looksLikeBank = BankStatementIndicators.IsMatch(text);

        if (looksLikeCreditCard && !looksLikeBank)
        {
            return DocumentType.CreditCardStatement;
        }

        if (looksLikeBank && !looksLikeCreditCard)
        {
            return DocumentType.BankStatement;
        }

        return null;
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
