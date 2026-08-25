using System.Security.Claims;
using FinancialStatementAI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinancialStatementAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/statements")]
public class StatementsController(IStatementService statementService, IStatementProcessingService processingService) : ControllerBase
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

    /// <summary>Runs the currently-available processing steps (text extraction, transaction
    /// parsing/classification, reconciliation as of Phase 11) synchronously and returns the
    /// updated statement. Phase 14 moves this to a Hangfire background job (returning 202
    /// Accepted instead) without changing the URL/verb.</summary>
    [HttpPost("{id:guid}/reprocess")]
    public async Task<IActionResult> Reprocess(Guid id, CancellationToken cancellationToken)
    {
        var statement = await processingService.ProcessAsync(id, CurrentUserId, cancellationToken);
        return statement is null ? NotFound() : Ok(statement);
    }

    /// <summary>The most recent reconciliation run for this statement. 404 both when the
    /// statement doesn't exist/isn't yours, and when it exists but hasn't been reconciled yet
    /// (no reprocess run to completion) — the client can already tell the difference from the
    /// statement's own processingStatus.</summary>
    [HttpGet("{id:guid}/reconciliation")]
    public async Task<IActionResult> GetReconciliation(Guid id, CancellationToken cancellationToken)
    {
        var reconciliation = await statementService.GetReconciliationAsync(id, CurrentUserId, cancellationToken);
        return reconciliation is null ? NotFound() : Ok(reconciliation);
    }

    /// <summary>Marks a statement Verified once a human reviewer is satisfied with its
    /// AI-classified transactions and reconciliation result (Phase 12). Only valid from
    /// PendingReview — reprocessing a Verified statement moves it back through the pipeline
    /// (ending at PendingReview again), so verification is never a dead end.</summary>
    [HttpPost("{id:guid}/verify")]
    public async Task<IActionResult> Verify(Guid id, CancellationToken cancellationToken)
    {
        var result = await statementService.VerifyAsync(id, CurrentUserId, cancellationToken);
        if (result.NotFound)
        {
            return NotFound();
        }

        if (!result.Succeeded)
        {
            return BadRequest(new ProblemDetails { Title = "Verification rejected", Detail = result.Error, Status = StatusCodes.Status400BadRequest });
        }

        return Ok(result.Statement);
    }
}
