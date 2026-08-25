using System.Security.Claims;
using FinancialStatementAI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinancialStatementAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/statements")]
public class StatementsController(IStatementService statementService) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("upload")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails { Title = "No file was uploaded.", Status = StatusCodes.Status400BadRequest });
        }

        await using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, cancellationToken);

        var result = await statementService.UploadAsync(
            CurrentUserId,
            memoryStream.ToArray(),
            file.FileName,
            file.Length,
            cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new ProblemDetails { Title = "Upload rejected", Detail = result.Error, Status = StatusCodes.Status400BadRequest });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Statement!.Id }, result.Statement);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var statements = await statementService.GetForUserAsync(CurrentUserId, cancellationToken);
        return Ok(statements);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var statement = await statementService.GetByIdAsync(id, CurrentUserId, cancellationToken);
        return statement is null ? NotFound() : Ok(statement);
    }

    [HttpGet("{id:guid}/status")]
    public async Task<IActionResult> GetStatus(Guid id, CancellationToken cancellationToken)
    {
        var status = await statementService.GetStatusAsync(id, CurrentUserId, cancellationToken);
        return status is null ? NotFound() : Ok(status);
    }
}
