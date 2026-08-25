namespace FinancialStatementAI.Domain.Constants;

/// <summary>The first, highest-priority rung of the hybrid classification ladder (requirement
/// #17) — structural keywords that identify a transaction's category from its own description,
/// independent of which merchant is involved (a "PAYROLL DEPOSIT" is Payroll no matter whose
/// payroll system issued it). Checked before merchant mapping precisely because these are more
/// reliable signals than a merchant name pattern could ever be for this class of transaction.</summary>
public static class ClassificationKeywordRules
{
    public static readonly IReadOnlyList<(string Keyword, string CategoryName)> Rules =
    [
        ("PAYROLL", "Payroll"),
        ("SALARY", "Payroll"),
        ("DIRECT DEPOSIT", "Income"),
        ("RENT PAYMENT", "Rent"),
        ("LOAN PAYMENT", "Loan Payment"),
        ("MORTGAGE", "Loan Payment"),
        ("OVERDRAFT FEE", "Bank Fee"),
        ("MAINTENANCE FEE", "Bank Fee"),
        ("ATM FEE", "Bank Fee"),
        ("SERVICE FEE", "Bank Fee"),
        ("INTEREST CHARGE", "Interest"),
        ("INTEREST PAYMENT", "Interest"),
        ("TAX PAYMENT", "Tax"),
        ("IRS", "Tax"),
        ("REFUND", "Refund"),
        ("TRANSFER TO", "Transfer"),
        ("TRANSFER FROM", "Transfer"),
        ("ELECTRIC", "Utilities"),
        ("WATER BILL", "Utilities"),
        ("GAS BILL", "Utilities"),
        ("INTERNET SERVICE", "Utilities")
    ];
}
