using System.Security.Claims;
using FinancialStatementAI.Application.DTOs.Categories;
using FinancialStatementAI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinancialStatementAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/categories")]
public class CategoriesController(ICategoryService categoryService) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Active categories, for the review UI's correction picker (Phase 12).</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var categories = await categoryService.GetActiveAsync(cancellationToken);
        return Ok(categories);
    }

    /// <summary>Every category including inactive ones, for the Categories management page
    /// (requirement 10) — as opposed to GetAll's review-picker-only active set.</summary>
    [HttpGet("all")]
    public async Task<IActionResult> GetAllIncludingInactive(CancellationToken cancellationToken)
    {
        var categories = await categoryService.GetAllAsync(cancellationToken);
        return Ok(categories);
    }

    /// <summary>Per-category transaction count/total spend/AI-vs-human-corrected split for the
    /// current user's own transactions (requirement 10's category cards + distribution chart).</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var stats = await categoryService.GetStatsAsync(CurrentUserId, cancellationToken);
        return Ok(stats);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var category = await categoryService.GetByIdAsync(id, cancellationToken);
        return category is null ? NotFound() : Ok(category);
    }

    /// <summary>Admin-only — reshaping the category taxonomy itself is a different kind of action
    /// than a Reviewer correcting one transaction's category.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await categoryService.CreateAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(new ProblemDetails { Title = "Category creation rejected", Detail = result.Error, Status = StatusCodes.Status400BadRequest });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Category!.Id }, result.Category);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await categoryService.UpdateAsync(id, request, cancellationToken);
        if (result.NotFound)
        {
            return NotFound();
        }

        if (!result.Succeeded)
        {
            return BadRequest(new ProblemDetails { Title = "Category update rejected", Detail = result.Error, Status = StatusCodes.Status400BadRequest });
        }

        return Ok(result.Category);
    }

    /// <summary>Soft-delete only (IsActive = false) — never a hard delete, since transactions
    /// reference CategoryId and a category's history must stay inspectable.</summary>
    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await categoryService.SetActiveAsync(id, false, cancellationToken);
        return result.NotFound ? NotFound() : Ok(result.Category);
    }

    [HttpPost("{id:guid}/reactivate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await categoryService.SetActiveAsync(id, true, cancellationToken);
        return result.NotFound ? NotFound() : Ok(result.Category);
    }
}
