using FinancialStatementAI.Application.DTOs.Common;
using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Domain.Enums;
using FinancialStatementAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinancialStatementAI.Infrastructure.Repositories;

public class ReconciliationRepository(AppDbContext dbContext) : IReconciliationRepository
{
    public async Task AddAsync(ReconciliationResult result, CancellationToken cancellationToken = default)
    {
        dbContext.ReconciliationResults.Add(result);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<ReconciliationResult?> GetLatestAsync(Guid statementId, CancellationToken cancellationToken = default) =>
        dbContext.ReconciliationResults
            .Where(r => r.StatementId == statementId)
            .OrderByDescending(r => r.CreatedAt)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<(ReconciliationResult Result, Statement Statement)>> GetCurrentForUserAsync(
        Guid userId, ReconciliationStatus? status, string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var current = await GetCurrentPerStatementAsync(userId, cancellationToken);

        if (status.HasValue)
        {
            current = current.Where(x => x.Result.Status == status.Value).ToList();
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            current = current.Where(x => x.Statement.OriginalFileName.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var ordered = current.OrderByDescending(x => x.Result.CreatedAt).ToList();
        var totalCount = ordered.Count;
        var pageItems = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return PagedResult<(ReconciliationResult, Statement)>.Create(pageItems, totalCount, page, pageSize);
    }

    public async Task<(int Reconciled, int Mismatch, int InsufficientInformation, int Pending, decimal TotalMismatchDiscrepancy)> GetSummaryCountsAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var totalStatements = await dbContext.Statements.CountAsync(s => s.UserId == userId, cancellationToken);
        var current = await GetCurrentPerStatementAsync(userId, cancellationToken);

        var reconciled = current.Count(x => x.Result.Status == ReconciliationStatus.Reconciled);
        var mismatch = current.Count(x => x.Result.Status == ReconciliationStatus.Mismatch);
        var insufficientInformation = current.Count(x => x.Result.Status == ReconciliationStatus.InsufficientInformation);
        var pending = totalStatements - current.Count;
        var totalDiscrepancy = current.Where(x => x.Result.Status == ReconciliationStatus.Mismatch).Sum(x => Math.Abs(x.Result.Discrepancy ?? 0m));

        return (reconciled, mismatch, insufficientInformation, pending, totalDiscrepancy);
    }

    /// <summary>Every one of the user's statements' current (most recent) reconciliation result —
    /// materialized and grouped in memory rather than a GroupBy+OrderBy+First SQL translation,
    /// since per-user reconciliation history is small (bounded by statement count) and this keeps
    /// both callers above simple and correct rather than fighting EF Core's GroupBy quirks for a
    /// marginal efficiency gain that doesn't matter at this scale.</summary>
    private async Task<List<(ReconciliationResult Result, Statement Statement)>> GetCurrentPerStatementAsync(Guid userId, CancellationToken cancellationToken)
    {
        var all = await dbContext.ReconciliationResults
            .Include(r => r.Statement)
            .Where(r => r.Statement!.UserId == userId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return all
            .GroupBy(r => r.StatementId)
            .Select(g =>
            {
                var latest = g.OrderByDescending(r => r.CreatedAt).First();
                return (latest, latest.Statement!);
            })
            .ToList();
    }
}
