namespace FinancialStatementAI.Domain.Constants;

/// <summary>
/// System-defined starter categories seeded into the database. Categories remain a fully
/// extensible/editable entity (see Category) — this list only seeds sensible defaults so
/// classification has somewhere to land on day one.
/// </summary>
public static class DefaultCategories
{
    public static readonly IReadOnlyList<string> Names =
    [
        "Food & Dining",
        "Groceries",
        "Transportation",
        "Fuel",
        "Travel",
        "Accommodation",
        "Shopping",
        "Software & SaaS",
        "Utilities",
        "Insurance",
        "Healthcare",
        "Payroll",
        "Rent",
        "Loan Payment",
        "Bank Fee",
        "Interest",
        "Tax",
        "Transfer",
        "Refund",
        "Income",
        "Other"
    ];
}
