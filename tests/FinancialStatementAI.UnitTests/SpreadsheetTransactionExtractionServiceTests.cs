using ClosedXML.Excel;
using FinancialStatementAI.Domain.Enums;
using FinancialStatementAI.Infrastructure.Documents;

namespace FinancialStatementAI.UnitTests;

public class SpreadsheetTransactionExtractionServiceTests
{
    private readonly SpreadsheetTransactionExtractionService _service = new();

    // Mirrors the real reference file (Transactions.xlsx): "# | Transaction ID | Category |
    // Description | Type | Amount (₹)" with NO date column at all, and a Type column that
    // determines direction for a single Amount column.
    private static MemoryStream BuildWorkbook(Action<IXLWorksheet> populate)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Sheet1");
        populate(worksheet);

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void Extract_Reads_The_Reference_File_Shape_With_A_Single_Type_Discriminated_Amount_Column()
    {
        using var stream = BuildWorkbook(ws =>
        {
            ws.Cell(1, 1).Value = "#";
            ws.Cell(1, 2).Value = "Transaction ID";
            ws.Cell(1, 3).Value = "Category";
            ws.Cell(1, 4).Value = "Description";
            ws.Cell(1, 5).Value = "Type";
            ws.Cell(1, 6).Value = "Amount (₹)";

            ws.Cell(2, 1).Value = 4;
            ws.Cell(2, 2).Value = "TXN1004";
            ws.Cell(2, 3).Value = "Food & Dining";
            ws.Cell(2, 4).Value = "Swiggy food order";
            ws.Cell(2, 5).Value = "Debit";
            ws.Cell(2, 6).Value = 425;

            ws.Cell(3, 1).Value = 60;
            ws.Cell(3, 2).Value = "TXN1060";
            ws.Cell(3, 3).Value = "Loan";
            ws.Cell(3, 4).Value = "Loan disbursement";
            ws.Cell(3, 5).Value = "Credit";
            ws.Cell(3, 6).Value = 250000;
        });

        var result = _service.Extract(stream);

        Assert.Equal(2, result.Count);

        var debit = result[0];
        Assert.Null(debit.TransactionDate); // no date column at all — never fabricated
        Assert.Equal("TXN1004", debit.ReferenceNumber);
        Assert.Equal("Swiggy food order", debit.Description);
        Assert.Equal(-425m, debit.Amount);
        Assert.Equal(425m, debit.DebitAmount);
        Assert.Equal(TransactionType.Debit, debit.TransactionType);
        Assert.Equal("INR", debit.Currency); // detected from the "Amount (₹)" header

        var credit = result[1];
        Assert.Equal(250000m, credit.Amount);
        Assert.Equal(TransactionType.Credit, credit.TransactionType);
    }

    [Fact]
    public void Extract_Tolerates_Real_World_Typos_In_The_Type_Column()
    {
        using var stream = BuildWorkbook(ws =>
        {
            ws.Cell(1, 1).Value = "Description";
            ws.Cell(1, 2).Value = "Type";
            ws.Cell(1, 3).Value = "Amount";

            ws.Cell(2, 1).Value = "Online course fee";
            ws.Cell(2, 2).Value = "Dbit";
            ws.Cell(2, 3).Value = 2999;
        });

        var transaction = Assert.Single(_service.Extract(stream));

        Assert.Equal(-2999m, transaction.Amount);
        Assert.Equal(TransactionType.Debit, transaction.TransactionType);
    }

    [Fact]
    public void Extract_Reads_Separate_Debit_And_Credit_Columns()
    {
        using var stream = BuildWorkbook(ws =>
        {
            ws.Cell(1, 1).Value = "Date";
            ws.Cell(1, 2).Value = "Description";
            ws.Cell(1, 3).Value = "Debit";
            ws.Cell(1, 4).Value = "Credit";

            ws.Cell(2, 1).Value = new DateTime(2026, 3, 2);
            ws.Cell(2, 2).Value = "PAYROLL DIRECT DEPOSIT";
            ws.Cell(2, 3).Value = string.Empty;
            ws.Cell(2, 4).Value = 2300.00;

            ws.Cell(3, 1).Value = new DateTime(2026, 3, 3);
            ws.Cell(3, 2).Value = "WHOLE FOODS MARKET";
            ws.Cell(3, 3).Value = 86.42;
            ws.Cell(3, 4).Value = string.Empty;
        });

        var result = _service.Extract(stream);

        Assert.Equal(2, result.Count);
        Assert.Equal(new DateOnly(2026, 3, 2), result[0].TransactionDate);
        Assert.Equal(2300.00m, result[0].Amount);
        Assert.Equal(TransactionType.Credit, result[0].TransactionType);

        Assert.Equal(-86.42m, result[1].Amount);
        Assert.Equal(TransactionType.Debit, result[1].TransactionType);
    }

    [Fact]
    public void Extract_Skips_A_Row_With_No_Recognizable_Amount()
    {
        using var stream = BuildWorkbook(ws =>
        {
            ws.Cell(1, 1).Value = "Description";
            ws.Cell(1, 2).Value = "Amount";

            ws.Cell(2, 1).Value = "Some note with no amount";
            ws.Cell(2, 2).Value = string.Empty;
        });

        Assert.Empty(_service.Extract(stream));
    }

    [Fact]
    public void Extract_Returns_Empty_When_No_Amount_Bearing_Column_Exists_At_All()
    {
        using var stream = BuildWorkbook(ws =>
        {
            ws.Cell(1, 1).Value = "Description";
            ws.Cell(1, 2).Value = "Notes";

            ws.Cell(2, 1).Value = "Some transaction";
            ws.Cell(2, 2).Value = "Nothing usable here";
        });

        Assert.Empty(_service.Extract(stream));
    }
}
