using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Application.Services;

/// <summary>Pure, deterministic decimal arithmetic — no AI/LLM call anywhere in this class. See
/// requirement #20: financial reconciliation must never be delegated to an AI model.</summary>
public class ReconciliationService(
    IStatementRepository statementRepository,
    IReconciliationRepository reconciliationRepository) : IReconciliationService
{
    // Small tolerance for rounding artifacts in extracted amounts, not a business fudge factor.
    private const decimal Tolerance = 0.01m;

    public async Task<ReconciliationResponse> ReconcileAsync(Guid statementId, CancellationToken cancellationToken = default)
    {
        var statement = await statementRepository.GetByIdAsync(statementId, cancellationToken)
            ?? throw new InvalidOperationException($"Statement {statementId} was not found.");

        var totalCredits = statement.Transactions.Where(t => t.Amount is > 0).Sum(t => t.Amount!.Value);
        var totalDebits = statement.Transactions.Where(t => t.Amount is < 0).Sum(t => Math.Abs(t.Amount!.Value));

        decimal? expectedClosingBalance = null;
        decimal? discrepancy = null;
        ReconciliationStatus status;
        string? notes;

        if (statement.OpeningBalance is null || statement.ClosingBalance is null)
        {
            status = ReconciliationStatus.InsufficientInformation;
            notes = "Opening balance and/or closing balance could not be determined from the statement, so the expected closing balance cannot be checked.";
        }
        else
        {
            expectedClosingBalance = statement.OpeningBalance.Value + totalCredits - totalDebits;
            discrepancy = expectedClosingBalance.Value - statement.ClosingBalance.Value;

            if (Math.Abs(discrepancy.Value) <= Tolerance)
            {
                status = ReconciliationStatus.Reconciled;
                notes = null;
            }
            else
            {
                status = ReconciliationStatus.Mismatch;
                notes = $"Opening balance ({statement.OpeningBalance:F2}) + credits ({totalCredits:F2}) - debits ({totalDebits:F2}) " +
                        $"= expected closing balance of {expectedClosingBalance:F2}, but the statement reports a closing balance of " +
                        $"{statement.ClosingBalance:F2} (discrepancy: {discrepancy:F2}).";
            }
        }

        var result = new ReconciliationResult
        {
            StatementId = statementId,
            OpeningBalance = statement.OpeningBalance,
            TotalCredits = totalCredits,
            TotalDebits = totalDebits,
            ExpectedClosingBalance = expectedClosingBalance,
            StatementClosingBalance = statement.ClosingBalance,
            Discrepancy = discrepancy,
            Status = status,
            Notes = notes
        };

        await reconciliationRepository.AddAsync(result, cancellationToken);

        return ToResponse(result);
    }

    internal static ReconciliationResponse ToResponse(ReconciliationResult result) => new()
    {
        Id = result.Id,
        OpeningBalance = result.OpeningBalance,
        TotalCredits = result.TotalCredits,
        TotalDebits = result.TotalDebits,
        ExpectedClosingBalance = result.ExpectedClosingBalance,
        StatementClosingBalance = result.StatementClosingBalance,
        Discrepancy = result.Discrepancy,
        Status = result.Status.ToString(),
        Notes = result.Notes,
        CreatedAt = result.CreatedAt
    };
}
