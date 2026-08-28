using System.Security.Claims;
using FinancialStatementAI.Application.DTOs.Transactions;
using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinancialStatementAI.Api.Controllers;

/// <summary>XLSX/PDF export for each of the five data areas — reuses the same filters and
/// user-scoping as the underlying list endpoints (StatementsController, TransactionsController,
/// ReconciliationController, CategoriesController); this controller only decides output format
/// and the download's Content-Type/Content-Disposition.</summary>
[ApiController]
[Authorize]
[Route("api/reports")]
public class ReportsController(IReportGenerationService reportGenerationService) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("statements")]
    public async Task<IActionResult> GetStatementsReport(
        [FromQuery] string? search,
        [FromQuery] StatementProcessingStatus? status,
        [FromQuery] ReconciliationStatus? reconciliationStatus,
        [FromQuery] string format = "xlsx",
        CancellationToken cancellationToken = default)
    {
        if (!TryParseFormat(format, out var reportFormat))
        {
            return InvalidFormat();
        }

        var content = await reportGenerationService.GenerateStatementsReportAsync(
            CurrentUserId, search, status, reconciliationStatus, reportFormat, cancellationToken);
        return BuildFileResult(content, reportFormat, "statements");
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactionsReport(
        [FromQuery] TransactionSearchFilter filter,
        [FromQuery] string format = "xlsx",
        CancellationToken cancellationToken = default)
    {
        if (!TryParseFormat(format, out var reportFormat))
        {
            return InvalidFormat();
        }

        var content = await reportGenerationService.GenerateTransactionsReportAsync(CurrentUserId, filter, reportFormat, cancellationToken);
        return BuildFileResult(content, reportFormat, "transactions");
    }

    /// <summary>The classification review queue report — same data GetReviewQueue serves.</summary>
    [HttpGet("review")]
    public async Task<IActionResult> GetReviewReport([FromQuery] string format = "xlsx", CancellationToken cancellationToken = default)
    {
        if (!TryParseFormat(format, out var reportFormat))
        {
            return InvalidFormat();
        }

        var content = await reportGenerationService.GenerateReviewQueueReportAsync(CurrentUserId, reportFormat, cancellationToken);
        return BuildFileResult(content, reportFormat, "review-queue");
    }

    [HttpGet("reconciliation")]
    public async Task<IActionResult> GetReconciliationReport(
        [FromQuery] ReconciliationStatus? status,
        [FromQuery] string? search,
        [FromQuery] string format = "xlsx",
        CancellationToken cancellationToken = default)
    {
        if (!TryParseFormat(format, out var reportFormat))
        {
            return InvalidFormat();
        }

        var content = await reportGenerationService.GenerateReconciliationReportAsync(CurrentUserId, status, search, reportFormat, cancellationToken);
        return BuildFileResult(content, reportFormat, "reconciliation");
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategoriesReport([FromQuery] string format = "xlsx", CancellationToken cancellationToken = default)
    {
        if (!TryParseFormat(format, out var reportFormat))
        {
            return InvalidFormat();
        }

        var content = await reportGenerationService.GenerateCategoriesReportAsync(CurrentUserId, reportFormat, cancellationToken);
        return BuildFileResult(content, reportFormat, "categories");
    }

    private BadRequestObjectResult InvalidFormat() =>
        BadRequest(new ProblemDetails { Title = "Invalid format", Detail = "format must be \"xlsx\" or \"pdf\".", Status = StatusCodes.Status400BadRequest });

    private static bool TryParseFormat(string format, out ReportFormat reportFormat) =>
        Enum.TryParse(format, ignoreCase: true, out reportFormat) && Enum.IsDefined(reportFormat);

    private static FileStreamResult BuildFileResult(byte[] content, ReportFormat format, string areaName)
    {
        var contentType = format == ReportFormat.Xlsx
            ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            : "application/pdf";
        var extension = format == ReportFormat.Xlsx ? "xlsx" : "pdf";
        var fileName = $"{areaName}-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.{extension}";

        return new FileStreamResult(new MemoryStream(content), contentType) { FileDownloadName = fileName };
    }
}
