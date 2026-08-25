using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Domain.Constants;

/// <summary>Seed data for system-defined merchant mappings — a starter set covering the exact
/// examples from the challenge doc ("UBER *TRIP" -> Transportation, "WHOLE FOODS" -> Groceries,
/// "AWS EMEA" -> Software &amp; SaaS, "DELTA AIR" -> Travel) plus other common, unambiguous
/// merchants. Deliberately not exhaustive — see requirement #7: the LLM classifier exists
/// precisely to handle merchants that aren't in this list.</summary>
public static class DefaultMerchantMappings
{
    public static readonly IReadOnlyList<(string Pattern, string CategoryName)> Mappings =
    [
        ("UBER", "Transportation"),
        ("LYFT", "Transportation"),
        ("WHOLE FOODS", "Groceries"),
        ("TRADER JOE", "Groceries"),
        ("KROGER", "Groceries"),
        ("SAFEWAY", "Groceries"),
        ("AWS", "Software & SaaS"),
        ("AMAZON WEB SERVICES", "Software & SaaS"),
        ("MICROSOFT 365", "Software & SaaS"),
        ("GITHUB", "Software & SaaS"),
        ("DELTA AIR", "Travel"),
        ("UNITED AIRLINES", "Travel"),
        ("AMERICAN AIRLINES", "Travel"),
        ("MARRIOTT", "Accommodation"),
        ("HILTON", "Accommodation"),
        ("AIRBNB", "Accommodation"),
        ("SHELL", "Fuel"),
        ("CHEVRON", "Fuel"),
        ("EXXON", "Fuel"),
        ("NETFLIX", "Software & SaaS"),
        ("SPOTIFY", "Software & SaaS"),
        ("STARBUCKS", "Food & Dining"),
        ("MCDONALD", "Food & Dining"),
        ("CHIPOTLE", "Food & Dining"),
        ("DOORDASH", "Food & Dining"),
        ("AMAZON", "Shopping"),
        ("WALMART", "Shopping"),
        ("TARGET", "Shopping"),
        ("GEICO", "Insurance"),
        ("STATE FARM", "Insurance"),
        ("CVS PHARMACY", "Healthcare"),
        ("WALGREENS", "Healthcare")
    ];
}
