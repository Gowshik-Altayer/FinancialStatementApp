using ClosedXML.Excel;
using FinancialStatementAI.Application.DTOs.Common;
using FinancialStatementAI.Application.DTOs.Transactions;
using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Constants;
using FinancialStatementAI.Domain.Enums;
using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FinancialStatementAI.Infrastructure.Reports;

/// <summary>Renders Statements/Transactions/Review/Reconciliation/Categories as XLSX (ClosedXML)
/// or PDF (QuestPDF) reports. Deliberately calls the same Application-layer services the
/// corresponding controllers already use — user-scoping and filtering logic lives in exactly one
/// place, never duplicated here. The underlying services are paginated for on-screen display, so
/// <see cref="FetchAllPagesAsync{T}"/> pages through them internally to build a report containing
/// every matching row, not just one page.</summary>
public class ReportGenerationService(
    IStatementService statementService,
    ITransactionService transactionService,
    IReconciliationService reconciliationService,
    ICategoryService categoryService) : IReportGenerationService
{
    // QuestPDF requires its license set exactly once per process before generating any document.
    // Set here (rather than only in DependencyInjection's composition root) so it applies whether
    // this service is constructed via DI at API startup or directly in a unit test — Community is
    // free for this project's size/revenue profile (see QuestPDF's licensing page).
    static ReportGenerationService()
    {
        Settings.License = LicenseType.Community;
    }

    private static readonly string[] TransactionHeaders =
    [
        "Date", "Posting Date", "Description", "Merchant", "Reference", "Debit", "Credit", "Amount",
        "Currency", "Type", "Category", "Confidence", "Review Priority", "Corrected", "Statement"
    ];

    public async Task<byte[]> GenerateStatementsReportAsync(
        Guid userId,
        string? search,
        StatementProcessingStatus? status,
        ReconciliationStatus? reconciliationStatus,
        ReportFormat format,
        CancellationToken cancellationToken = default)
    {
        var statements = await FetchAllPagesAsync(
            (page, pageSize, ct) => statementService.SearchAsync(userId, search, status, reconciliationStatus, page, pageSize, ct),
            cancellationToken);

        string[] headers =
        [
            "File Name", "Provider", "Period Start", "Period End", "Transactions",
            "Total Debits", "Total Credits", "Processing Status", "Reconciliation Status", "Uploaded At"
        ];
        var rows = statements.Select(s => new object?[]
        {
            s.OriginalFileName, s.ProviderName, s.StatementPeriodStart, s.StatementPeriodEnd,
            s.TransactionCount, s.TotalDebits, s.TotalCredits, s.ProcessingStatus, s.ReconciliationStatus, s.UploadedAt
        }).ToList();

        return Render("Statements", headers, rows, format);
    }

    public async Task<byte[]> GenerateTransactionsReportAsync(
        Guid userId, TransactionSearchFilter filter, ReportFormat format, CancellationToken cancellationToken = default)
    {
        var transactions = await FetchAllPagesAsync(
            (page, pageSize, ct) =>
            {
                filter.Page = page;
                filter.PageSize = pageSize;
                return transactionService.SearchAsync(userId, filter, ct);
            },
            cancellationToken);

        return Render("Transactions", TransactionHeaders, transactions.Select(TransactionRow).ToList(), format);
    }

    public async Task<byte[]> GenerateReviewQueueReportAsync(Guid userId, ReportFormat format, CancellationToken cancellationToken = default)
    {
        var transactions = await transactionService.GetReviewQueueAsync(userId, cancellationToken);
        return Render("Review Queue", TransactionHeaders, transactions.Select(TransactionRow).ToList(), format);
    }

    public async Task<byte[]> GenerateReconciliationReportAsync(
        Guid userId, ReconciliationStatus? status, string? search, ReportFormat format, CancellationToken cancellationToken = default)
    {
        var results = await FetchAllPagesAsync(
            (page, pageSize, ct) => reconciliationService.GetSummaryForUserAsync(userId, status, search, page, pageSize, ct),
            cancellationToken);

        string[] headers =
        [
            "Statement", "Opening Balance", "Total Credits", "Total Debits",
            "Expected Closing", "Actual Closing", "Discrepancy", "Status", "Notes", "Created At"
        ];
        var rows = results.Select(r => new object?[]
        {
            r.StatementFileName, r.OpeningBalance, r.TotalCredits, r.TotalDebits,
            r.ExpectedClosingBalance, r.StatementClosingBalance, r.Discrepancy, r.Status, r.Notes, r.CreatedAt
        }).ToList();

        return Render("Reconciliation", headers, rows, format);
    }

    public async Task<byte[]> GenerateCategoriesReportAsync(Guid userId, ReportFormat format, CancellationToken cancellationToken = default)
    {
        var categories = await categoryService.GetAllAsync(cancellationToken);
        var statsById = (await categoryService.GetStatsAsync(userId, cancellationToken)).ToDictionary(s => s.CategoryId);

        string[] headers =
            ["Name", "Description", "Active", "System Defined", "Transactions", "Total Amount", "AI Classified %", "Human Corrected %"];
        var rows = categories.Select(c =>
        {
            statsById.TryGetValue(c.Id, out var stats);
            return new object?[]
            {
                c.Name, c.Description, c.IsActive, c.IsSystemDefined,
                stats?.TransactionCount ?? 0, stats?.TotalAmount ?? 0m, stats?.AiClassifiedPercent, stats?.HumanCorrectedPercent
            };
        }).ToList();

        return Render("Categories", headers, rows, format);
    }

    private static object?[] TransactionRow(TransactionResponse t) =>
    [
        t.TransactionDate, t.PostingDate, t.Description, t.Merchant, t.ReferenceNumber,
        t.DebitAmount, t.CreditAmount, t.Amount, t.Currency, t.TransactionType,
        t.CategoryName, t.ClassificationConfidence, t.ReviewPriority, t.HasBeenCorrected, t.StatementFileName
    ];

    private static byte[] Render(string title, string[] headers, IReadOnlyList<object?[]> rows, ReportFormat format) =>
        format == ReportFormat.Xlsx ? BuildXlsx(title, headers, rows) : BuildPdf(title, headers, rows);

    /// <summary>Repeatedly calls a paginated fetch delegate at <see cref="PaginationDefaults.MaxPageSize"/>
    /// per page until every matching row has been collected — the report is always the full
    /// filtered result set, never just the first page a list UI would show.</summary>
    private static async Task<List<T>> FetchAllPagesAsync<T>(
        Func<int, int, CancellationToken, Task<PagedResult<T>>> fetchPage, CancellationToken cancellationToken)
    {
        var all = new List<T>();
        var page = 1;
        while (true)
        {
            var result = await fetchPage(page, PaginationDefaults.MaxPageSize, cancellationToken);
            all.AddRange(result.Items);
            if (result.Items.Count == 0 || all.Count >= result.TotalCount)
            {
                break;
            }

            page++;
        }

        return all;
    }

    private static byte[] BuildXlsx(string sheetName, string[] headers, IReadOnlyList<object?[]> rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName);

        for (var col = 0; col < headers.Length; col++)
        {
            var cell = worksheet.Cell(1, col + 1);
            cell.Value = headers[col];
            cell.Style.Font.Bold = true;
        }

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            for (var col = 0; col < row.Length; col++)
            {
                SetCellValue(worksheet.Cell(rowIndex + 2, col + 1), row[col]);
            }
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void SetCellValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                break;
            case string s:
                cell.Value = s;
                break;
            case bool b:
                cell.Value = b;
                break;
            case int i:
                cell.Value = i;
                break;
            case decimal d:
                cell.Value = d;
                break;
            case DateTime dt:
                cell.Value = dt;
                break;
            case DateOnly d2:
                cell.Value = d2.ToDateTime(TimeOnly.MinValue);
                break;
            default:
                cell.Value = value.ToString();
                break;
        }
    }

    private static byte[] BuildPdf(string title, string[] headers, IReadOnlyList<object?[]> rows)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Text(title).FontSize(16).SemiBold();

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        foreach (var _ in headers)
                        {
                            columns.RelativeColumn();
                        }
                    });

                    table.Header(header =>
                    {
                        foreach (var text in headers)
                        {
                            header.Cell().Element(HeaderCellStyle).Text(text).Bold();
                        }
                    });

                    foreach (var row in rows)
                    {
                        foreach (var value in row)
                        {
                            table.Cell().Element(RowCellStyle).Text(FormatValue(value));
                        }
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();

        static IContainer HeaderCellStyle(IContainer c) =>
            c.PaddingVertical(4).BorderBottom(1).BorderColor(Colors.Grey.Darken1);

        static IContainer RowCellStyle(IContainer c) =>
            c.PaddingVertical(3).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2);
    }

    private static string FormatValue(object? value) => value switch
    {
        null => "—",
        decimal d => d.ToString("N2"),
        DateOnly d => d.ToString("yyyy-MM-dd"),
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm"),
        bool b => b ? "Yes" : "No",
        _ => value.ToString() ?? string.Empty
    };
}
