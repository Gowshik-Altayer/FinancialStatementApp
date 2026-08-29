using FinancialStatementAI.Domain.Enums;
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

    // --- Statement period / statement date (requirement #3) --------------------------------

    [Fact]
    public void Extract_Reads_The_Statement_Period_As_A_Slash_Date_Range()
    {
        var result = _service.Extract("Statement Period: 03/01/2026 - 03/31/2026\nSome other text");

        Assert.Equal(new DateOnly(2026, 3, 1), result.StatementPeriodStart);
        Assert.Equal(new DateOnly(2026, 3, 31), result.StatementPeriodEnd);
    }

    [Fact]
    public void Extract_Reads_The_Statement_Period_As_A_Month_Name_Date_Range()
    {
        var result = _service.Extract("Statement Period March 1, 2026 to March 31, 2026");

        Assert.Equal(new DateOnly(2026, 3, 1), result.StatementPeriodStart);
        Assert.Equal(new DateOnly(2026, 3, 31), result.StatementPeriodEnd);
    }

    [Fact]
    public void Extract_Leaves_The_Statement_Period_Null_When_Only_One_Date_Is_Found()
    {
        var result = _service.Extract("Statement Period: 03/01/2026 onward with no end date given");

        Assert.Null(result.StatementPeriodStart);
        Assert.Null(result.StatementPeriodEnd);
    }

    [Fact]
    public void Extract_Reads_The_Statement_Date()
    {
        var result = _service.Extract("Statement Date: 03/31/2026\nAccount Holder Name: Ada Lovelace");

        Assert.Equal(new DateOnly(2026, 3, 31), result.StatementDate);
    }

    // --- Document type identification (requirement #1) --------------------------------------

    [Fact]
    public void Extract_Identifies_A_Credit_Card_Statement()
    {
        var result = _service.Extract("Minimum Payment Due: $35.00\nPayment Due Date: 04/15/2026\nCredit Limit: $5,000.00");

        Assert.Equal(DocumentType.CreditCardStatement, result.DocumentType);
    }

    [Fact]
    public void Extract_Identifies_A_Bank_Statement()
    {
        var result = _service.Extract("Checking Account Summary\nRouting Number: 021000021");

        Assert.Equal(DocumentType.BankStatement, result.DocumentType);
    }

    [Fact]
    public void Extract_Leaves_Document_Type_Null_When_Neither_Vocabulary_Appears()
    {
        var result = _service.Extract("A generic document with no distinguishing statement vocabulary.");

        Assert.Null(result.DocumentType);
    }
}
