using System.Security.Claims;
using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Constants;
using FinancialStatementAI.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinancialStatementAI.Api.Controllers;

/// <summary>Cross-statement reconciliation (requirement 9) — as opposed to the existing
/// per-statement GET /api/statements/{id}/reconciliation, this covers every one of the current
/// user's statements at once, the data the new Reconciliation page needs.</summary>
[ApiController]
[Authorize]
[Route("api/reconciliation")]
public class ReconciliationController(IReconciliationService reconciliationService) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] ReconciliationStatus? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationDefaults.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await reconciliationService.GetSummaryForUserAsync(CurrentUserId, status, search, page, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>KPI/chart counts for the Reconciliation page — reconciled/mismatch/insufficient-
    /// information/pending counts plus the total dollar amount currently unaccounted for.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var summary = await reconciliationService.GetSummaryCountsAsync(CurrentUserId, cancellationToken);
        return Ok(summary);
    }
}
