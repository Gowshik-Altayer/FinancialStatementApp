using System.Security.Claims;
using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Constants;
using FinancialStatementAI.Domain.Enums;
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

    /// <summary>Search/filter/paginate the current user's statements (Phase 13). All query
    /// parameters are optional; omitting all of them returns the first page, most recently
    /// uploaded first — the same behavior the unpaginated endpoint had before this phase.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] StatementProcessingStatus? status,
        [FromQuery] ReconciliationStatus? reconciliationStatus,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationDefaults.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var statements = await statementService.SearchAsync(CurrentUserId, search, status, reconciliationStatus, page, pageSize, cancellationToken);
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

    /// <summary>Runs the processing pipeline (text extraction, transaction parsing/
    /// classification, reconciliation) for one statement — synchronously by default (200 OK with
    /// the final result), or enqueued for a separate Hangfire worker (202 Accepted, statement
    /// immediately flipped to Processing) when "BackgroundJobs:Provider" = "Hangfire" (Phase 14).
    /// The URL/verb never changes between the two; only the status code and how "finished" the
    /// returned snapshot is do.</summary>
    [HttpPost("{id:guid}/reprocess")]
    public async Task<IActionResult> Reprocess(Guid id, CancellationToken cancellationToken)
    {
        var statement = await statementService.RequestReprocessAsync(id, CurrentUserId, cancellationToken);
        if (statement is null)
        {
            return NotFound();
        }

        return statement.ProcessingStatus == nameof(StatementProcessingStatus.Processing)
            ? Accepted(statement)
            : Ok(statement);
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
