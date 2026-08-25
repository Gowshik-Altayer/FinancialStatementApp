using FinancialStatementAI.Domain.Enums;
using FinancialStatementAI.Infrastructure.Documents;

namespace FinancialStatementAI.UnitTests;

public class TransactionExtractionServiceTests
{
    private readonly TransactionExtractionService _service = new();
    private const int Year = 2026;

    [Fact]
    public void Extract_Parses_The_Slash_Date_Format_From_The_Challenge_Doc()
    {
        var result = _service.Extract("01/08 AMAZON WEB SERVICES 129.45", Year);

        var transaction = Assert.Single(result);
        Assert.Equal(new DateOnly(Year, 1, 8), transaction.TransactionDate);
        Assert.Equal("AMAZON WEB SERVICES", transaction.Description);
        Assert.Equal(129.45m, transaction.Amount);
        Assert.Equal(TransactionType.Credit, transaction.TransactionType); // no sign/keyword => positive => Credit
    }

    [Fact]
    public void Extract_Parses_The_Pipe_Delimited_Format_From_The_Challenge_Doc()
    {
        var result = _service.Extract("Aug 01 | Amazon Web Services | Debit | $129.45", Year);

        var transaction = Assert.Single(result);
        Assert.Equal(new DateOnly(Year, 8, 1), transaction.TransactionDate);
        Assert.Equal("Amazon Web Services", transaction.Description);
        Assert.Equal(-129.45m, transaction.Amount); // explicit Debit segment => negative
        Assert.Equal(129.45m, transaction.DebitAmount);
        Assert.Null(transaction.CreditAmount);
        Assert.Equal(TransactionType.Debit, transaction.TransactionType);
        Assert.Equal("USD", transaction.Currency);
    }

    [Fact]
    public void Extract_Parses_The_Dash_Date_Negative_Amount_Format_From_The_Challenge_Doc()
    {
        var result = _service.Extract("01-Aug AMAZON WEB SERVICES -129.45", Year);

        var transaction = Assert.Single(result);
        Assert.Equal(new DateOnly(Year, 8, 1), transaction.TransactionDate);
        Assert.Equal(-129.45m, transaction.Amount);
        Assert.Equal(TransactionType.Debit, transaction.TransactionType);
    }

    [Fact]
    public void Extract_Handles_Thousands_Separators_And_Parenthesized_Negatives()
    {
        var result = _service.Extract("03/15 LARGE PURCHASE (1,234.56)", Year);

        var transaction = Assert.Single(result);
        Assert.Equal(-1234.56m, transaction.Amount);
        Assert.Equal(TransactionType.Debit, transaction.TransactionType);
    }

    [Fact]
    public void Extract_Recognizes_Explicit_Keywords_Over_The_Bare_Sign()
    {
        var result = _service.Extract("04/01 REFUND FROM MERCHANT 50.00", Year);

        var transaction = Assert.Single(result);
        Assert.Equal(TransactionType.Credit, transaction.TransactionType);
    }

    [Fact]
    public void Extract_Merges_Wrapped_Multiline_Descriptions_Into_The_Preceding_Transaction()
    {
        // Realistic wrap: date + amount stay on the transaction's own line; the overflow line
        // has no amount of its own and must be recognized as pure description continuation.
        var rawText = "05/01 PAYMENT TO SOME VERY LONG MERCHANT 200.00\nNAME THAT WRAPPED TO A SECOND LINE";

        var result = _service.Extract(rawText, Year);

        var transaction = Assert.Single(result);
        Assert.Contains("PAYMENT TO SOME VERY LONG MERCHANT", transaction.Description);
        Assert.Contains("NAME THAT WRAPPED TO A SECOND LINE", transaction.Description);
        // "PAYMENT" is recognized as an explicit keyword (money out) — see ClassifyDirection.
        Assert.Equal(TransactionType.Payment, transaction.TransactionType);
        Assert.Equal(-200.00m, transaction.Amount);
    }

    [Fact]
    public void Extract_Skips_Lines_With_No_Recognizable_Amount()
    {
        var rawText = "STATEMENT FOR ACCOUNT 12345\n01/08 GROCERY STORE 45.67\nThank you for banking with us";

        var result = _service.Extract(rawText, Year);

        Assert.Single(result);
    }

    [Fact]
    public void Extract_Does_Not_Mistake_A_Bare_Reference_Number_For_An_Amount()
    {
        // No decimal point => not treated as an amount by design (see class doc comment) —
        // the whole line is skipped rather than fabricating a transaction with a wrong amount.
        var result = _service.Extract("01/08 SOME TRANSFER REF 123456", Year);

        Assert.Empty(result);
    }

    [Fact]
    public void Extract_Returns_Empty_For_Text_With_No_Transaction_Lines()
    {
        var result = _service.Extract("Just some header text\nAnd a footer\n", Year);

        Assert.Empty(result);
    }
}
