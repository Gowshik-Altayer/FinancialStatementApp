using System.Text;
using ClosedXML.Excel;
using FinancialStatementAI.Application.DTOs.Categories;
using FinancialStatementAI.Application.DTOs.Common;
using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Application.DTOs.Transactions;
using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Enums;
using FinancialStatementAI.Infrastructure.Reports;
using Moq;

namespace FinancialStatementAI.UnitTests;

public class ReportGenerationServiceTests
{
    private readonly Mock<IStatementService> _statementService = new();
    private readonly Mock<ITransactionService> _transactionService = new();
    private readonly Mock<IReconciliationService> _reconciliationService = new();
    private readonly Mock<ICategoryService> _categoryService = new();

    private ReportGenerationService CreateService() =>
        new(_statementService.Object, _transactionService.Object, _reconciliationService.Object, _categoryService.Object);

    private static bool LooksLikePdf(byte[] bytes) =>
        bytes.Length > 5 && Encoding.ASCII.GetString(bytes, 0, 5) == "%PDF-";

    [Fact]
    public async Task GenerateStatementsReportAsync_Xlsx_Contains_Header_And_Data_Rows()
    {
        var statement = new StatementSummaryResponse
        {
            Id = Guid.NewGuid(),
            OriginalFileName = "jan-statement.pdf",
            ProviderName = "Chase",
            TransactionCount = 3,
            TotalDebits = 120.50m,
            TotalCredits = 500m,
            ProcessingStatus = "Verified",
            ReconciliationStatus = "Reconciled",
            UploadedAt = new DateTime(2026, 1, 5)
        };
        _statementService
            .Setup(s => s.SearchAsync(It.IsAny<Guid>(), null, null, null, 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PagedResult<StatementSummaryResponse>.Create([statement], 1, 1, 100));

        var bytes = await CreateService().GenerateStatementsReportAsync(Guid.NewGuid(), null, null, null, ReportFormat.Xlsx);

        Assert.NotEmpty(bytes);
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var worksheet = workbook.Worksheets.First();
        Assert.Equal("File Name", worksheet.Cell(1, 1).GetString());
        Assert.Equal("jan-statement.pdf", worksheet.Cell(2, 1).GetString());
        Assert.Equal("Chase", worksheet.Cell(2, 2).GetString());
    }

    [Fact]
    public async Task GenerateStatementsReportAsync_Pdf_Produces_Valid_Pdf_Bytes()
    {
        _statementService
            .Setup(s => s.SearchAsync(It.IsAny<Guid>(), null, null, null, 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PagedResult<StatementSummaryResponse>.Create([], 0, 1, 100));

        var bytes = await CreateService().GenerateStatementsReportAsync(Guid.NewGuid(), null, null, null, ReportFormat.Pdf);

        Assert.NotEmpty(bytes);
        Assert.True(LooksLikePdf(bytes));
    }

    [Fact]
    public async Task GenerateTransactionsReportAsync_Pages_Through_Every_Matching_Transaction()
    {
        var page1Items = Enumerable.Range(0, 100).Select(i => new TransactionResponse { Id = Guid.NewGuid(), Description = $"Tx {i}" }).ToList();
        var page2Items = new List<TransactionResponse> { new() { Id = Guid.NewGuid(), Description = "Tx 100" } };

        _transactionService
            .Setup(s => s.SearchAsync(It.IsAny<Guid>(), It.Is<TransactionSearchFilter>(f => f.Page == 1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PagedResult<TransactionResponse>.Create(page1Items, 101, 1, 100));
        _transactionService
            .Setup(s => s.SearchAsync(It.IsAny<Guid>(), It.Is<TransactionSearchFilter>(f => f.Page == 2), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PagedResult<TransactionResponse>.Create(page2Items, 101, 2, 100));

        var bytes = await CreateService().GenerateTransactionsReportAsync(Guid.NewGuid(), new TransactionSearchFilter(), ReportFormat.Xlsx);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var worksheet = workbook.Worksheets.First();
        // Header row + 101 transactions across both pages, not just the first page's 100.
        Assert.Equal(102, worksheet.LastRowUsed()!.RowNumber());
    }

    [Fact]
    public async Task GenerateReviewQueueReportAsync_Xlsx_Lists_Every_Queued_Transaction()
    {
        _transactionService
            .Setup(s => s.GetReviewQueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TransactionResponse { Id = Guid.NewGuid(), Description = "Needs review", CategoryName = "Uncategorized" }]);

        var bytes = await CreateService().GenerateReviewQueueReportAsync(Guid.NewGuid(), ReportFormat.Xlsx);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var worksheet = workbook.Worksheets.First();
        Assert.Equal("Needs review", worksheet.Cell(2, 3).GetString());
    }

    [Fact]
    public async Task GenerateReconciliationReportAsync_Xlsx_Contains_Discrepancy_Rows()
    {
        var summary = new ReconciliationSummaryResponse
        {
            StatementId = Guid.NewGuid(),
            StatementFileName = "feb-statement.pdf",
            Status = "Mismatch",
            Discrepancy = 42.50m,
            CreatedAt = DateTime.UtcNow
        };
        _reconciliationService
            .Setup(s => s.GetSummaryForUserAsync(It.IsAny<Guid>(), null, null, 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PagedResult<ReconciliationSummaryResponse>.Create([summary], 1, 1, 100));

        var bytes = await CreateService().GenerateReconciliationReportAsync(Guid.NewGuid(), null, null, ReportFormat.Xlsx);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var worksheet = workbook.Worksheets.First();
        Assert.Equal("feb-statement.pdf", worksheet.Cell(2, 1).GetString());
        Assert.Equal("Mismatch", worksheet.Cell(2, 8).GetString());
    }

    [Fact]
    public async Task GenerateReconciliationReportAsync_Pdf_Produces_Valid_Pdf_Bytes()
    {
        _reconciliationService
            .Setup(s => s.GetSummaryForUserAsync(It.IsAny<Guid>(), null, null, 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PagedResult<ReconciliationSummaryResponse>.Create([], 0, 1, 100));

        var bytes = await CreateService().GenerateReconciliationReportAsync(Guid.NewGuid(), null, null, ReportFormat.Pdf);

        Assert.True(LooksLikePdf(bytes));
    }

    [Fact]
    public async Task GenerateCategoriesReportAsync_Joins_Category_With_Its_Own_Stats_By_Id()
    {
        var categoryId = Guid.NewGuid();
        _categoryService
            .Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CategoryDetailResponse { Id = categoryId, Name = "Groceries", IsActive = true }]);
        _categoryService
            .Setup(s => s.GetStatsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CategoryStatsResponse
            {
                CategoryId = categoryId,
                CategoryName = "Groceries",
                TransactionCount = 5,
                TotalAmount = 250m,
                AiClassifiedPercent = 80m,
                HumanCorrectedPercent = 20m
            }]);

        var bytes = await CreateService().GenerateCategoriesReportAsync(Guid.NewGuid(), ReportFormat.Xlsx);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var worksheet = workbook.Worksheets.First();
        Assert.Equal("Groceries", worksheet.Cell(2, 1).GetString());
        Assert.Equal(5, (int)worksheet.Cell(2, 5).GetDouble());
    }

    [Fact]
    public async Task GenerateCategoriesReportAsync_Never_Drops_A_Category_With_No_Transactions_Yet()
    {
        var categoryId = Guid.NewGuid();
        _categoryService
            .Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CategoryDetailResponse { Id = categoryId, Name = "Unused Category", IsActive = true }]);
        _categoryService
            .Setup(s => s.GetStatsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var bytes = await CreateService().GenerateCategoriesReportAsync(Guid.NewGuid(), ReportFormat.Xlsx);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var worksheet = workbook.Worksheets.First();
        Assert.Equal("Unused Category", worksheet.Cell(2, 1).GetString());
        Assert.Equal(0, (int)worksheet.Cell(2, 5).GetDouble());
    }

    [Fact]
    public async Task GenerateCategoriesReportAsync_Pdf_Produces_Valid_Pdf_Bytes()
    {
        _categoryService.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _categoryService.Setup(s => s.GetStatsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var bytes = await CreateService().GenerateCategoriesReportAsync(Guid.NewGuid(), ReportFormat.Pdf);

        Assert.True(LooksLikePdf(bytes));
    }
}
