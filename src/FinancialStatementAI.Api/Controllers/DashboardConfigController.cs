using System.Security.Claims;
using FinancialStatementAI.Application.DTOs.Dashboard;
using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinancialStatementAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard/config")]
public class DashboardConfigController(IDashboardConfigService dashboardConfigService) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private UserRole CurrentRole => Enum.Parse<UserRole>(User.FindFirstValue(ClaimTypes.Role)!);

    /// <summary>The current user's resolved dashboard layout — their role's defaults, with any of
    /// their own customizations layered on top.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMyConfig(CancellationToken cancellationToken)
    {
        var config = await dashboardConfigService.GetResolvedConfigAsync(CurrentUserId, CurrentRole, cancellationToken);
        return Ok(config);
    }

    /// <summary>Saves the current user's own widget visibility/order customizations — never
    /// touches the role defaults everyone else on that role still sees.</summary>
    [HttpPut]
    public async Task<IActionResult> UpdateMyConfig([FromBody] UpdateDashboardWidgetPreferencesRequest request, CancellationToken cancellationToken)
    {
        await dashboardConfigService.ReplaceUserOverridesAsync(CurrentUserId, request, cancellationToken);
        return NoContent();
    }

    /// <summary>What a given role sees by default, before any individual user's own
    /// customizations are applied — Admin-only, since this changes the experience for every user
    /// on that role at once.</summary>
    [HttpGet("role-defaults/{role}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetRoleDefaults(UserRole role, CancellationToken cancellationToken)
    {
        var config = await dashboardConfigService.GetRoleDefaultsAsync(role, cancellationToken);
        return Ok(config);
    }

    [HttpPut("role-defaults/{role}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateRoleDefaults(UserRole role, [FromBody] UpdateDashboardWidgetPreferencesRequest request, CancellationToken cancellationToken)
    {
        await dashboardConfigService.ReplaceRoleDefaultsAsync(role, request, cancellationToken);
        return NoContent();
    }
}
