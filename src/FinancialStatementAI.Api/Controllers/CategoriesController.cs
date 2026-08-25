using FinancialStatementAI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinancialStatementAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/categories")]
public class CategoriesController(ICategoryService categoryService) : ControllerBase
{
    /// <summary>Active categories, for the review UI's correction picker (Phase 12). Full
    /// category management (create/edit/deactivate) is a later phase — see README.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var categories = await categoryService.GetActiveAsync(cancellationToken);
        return Ok(categories);
    }
}
