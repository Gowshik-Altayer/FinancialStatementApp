using FinancialStatementAI.Infrastructure.Documents;

namespace FinancialStatementAI.UnitTests;

public class StatementFieldExtractionServiceTests
{
    private readonly StatementFieldExtractionService _service = new();

    [Fact]
    public void Extract_Finds_Balances_Near_Their_Labels()
    {
        var text = "Statement for John Smith\nOpening Balance $1,000.00\nClosing Balance $771.23\nTotal Debits $328.77\nTotal Credits $100.00";

        var result = _service.Extract(text);

        Assert.Equal(1000.00m, result.OpeningBalance);
        Assert.Equal(771.23m, result.ClosingBalance);
        Assert.Equal(328.77m, result.TotalDebits);
        Assert.Equal(100.00m, result.TotalCredits);
        Assert.Equal("USD", result.Currency);
    }

    [Fact]
    public void Extract_Returns_Null_Fields_When_Nothing_Is_Found_Rather_Than_Guessing()
    {
        var result = _service.Extract("This document has no recognizable statement fields at all.");

        Assert.Null(result.OpeningBalance);
        Assert.Null(result.ClosingBalance);
        Assert.Null(result.AccountHolderName);
        Assert.Null(result.ProviderName);
    }

    [Fact]
    public void Extract_Does_Not_Attribute_A_Distant_Amount_To_An_Unrelated_Label()
    {
        var text = "Opening Balance\n\n\n\n\n\nSome unrelated paragraph mentioning 999.99 much later.";

        var result = _service.Extract(text);

        Assert.Null(result.OpeningBalance);
    }
}
