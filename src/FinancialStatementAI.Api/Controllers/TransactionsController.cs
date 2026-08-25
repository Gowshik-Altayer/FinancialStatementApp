using System.Security.Claims;
using FinancialStatementAI.Application.DTOs.Transactions;
using FinancialStatementAI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinancialStatementAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public class TransactionsController(ITransactionService transactionService) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>All transactions for one statement (Phase 12 review grid) — same ownership rule
    /// as the statement endpoints (404 for another user's statement).</summary>
    [HttpGet("statements/{statementId:guid}/transactions")]
    public async Task<IActionResult> GetForStatement(Guid statementId, CancellationToken cancellationToken)
    {
        var transactions = await transactionService.GetForStatementAsync(statementId, CurrentUserId, cancellationToken);
        return transactions is null ? NotFound() : Ok(transactions);
    }

    /// <summary>The cross-statement human review queue: every transaction on one of the current
    /// user's PendingReview statements, lowest classification confidence first.</summary>
    [HttpGet("transactions/review-queue")]
    public async Task<IActionResult> GetReviewQueue(CancellationToken cancellationToken)
    {
        var transactions = await transactionService.GetReviewQueueAsync(CurrentUserId, cancellationToken);
        return Ok(transactions);
    }

    /// <summary>Applies a human's category correction (requirement #9) — the original AI-assigned
    /// category is preserved in the audit trail, never overwritten.</summary>
    [HttpPost("transactions/{transactionId:guid}/corrections")]
    public async Task<IActionResult> CorrectCategory(Guid transactionId, [FromBody] CorrectTransactionRequest request, CancellationToken cancellationToken)
    {
        var result = await transactionService.CorrectCategoryAsync(transactionId, CurrentUserId, request, cancellationToken);
        if (result.NotFound)
        {
            return NotFound();
        }

        if (!result.Succeeded)
        {
            return BadRequest(new ProblemDetails { Title = "Correction rejected", Detail = result.Error, Status = StatusCodes.Status400BadRequest });
        }

        return Ok(result.Transaction);
    }
}
