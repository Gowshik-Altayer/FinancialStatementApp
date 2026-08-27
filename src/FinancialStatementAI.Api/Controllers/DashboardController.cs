using System.Security.Claims;
using FinancialStatementAI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinancialStatementAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Every KPI, chart series, and the pipeline-stage funnel for the current user's
    /// data — an Admin sees every user's data, everyone else sees only their own (requirement:
    /// dashboard data must be real, never hardcoded). <paramref name="rangeDays"/> controls how
    /// many days the processing-trend chart covers.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] int rangeDays = 30, CancellationToken cancellationToken = default)
    {
        var summary = await dashboardService.GetSummaryAsync(CurrentUserId, User.IsInRole("Admin"), rangeDays, cancellationToken);
        return Ok(summary);
    }
}
