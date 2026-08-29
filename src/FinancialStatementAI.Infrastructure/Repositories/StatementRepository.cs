using FinancialStatementAI.Application.DTOs.Common;
using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Domain.Enums;
using FinancialStatementAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinancialStatementAI.Infrastructure.Repositories;

public class StatementRepository(AppDbContext dbContext) : IStatementRepository
{
    public async Task AddAsync(Statement statement, CancellationToken cancellationToken = default)
    {
        dbContext.Statements.Add(statement);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Statement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Statements
            .Include(s => s.Transactions)
            .Include(s => s.StatementExtraction)
            .Include(s => s.ReconciliationResults)
            .AsSplitQuery()
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<PagedResult<StatementSummaryResponse>> SearchForUserAsync(
        Guid userId,
        string? search,
        StatementProcessingStatus? status,
        ReconciliationStatus? reconciliationStatus,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Statements.Where(s => s.UserId == userId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(s =>
                s.OriginalFileName.ToLower().Contains(term) ||
                (s.ProviderName != null && s.ProviderName.ToLower().Contains(term)) ||
                (s.AccountHolderName != null && s.AccountHolderName.ToLower().Contains(term)));
        }

        if (status.HasValue)
        {
            query = query.Where(s => s.ProcessingStatus == status.Value);
        }

        if (reconciliationStatus.HasValue)
        {
            query = query.Where(s => s.ReconciliationResults
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => (ReconciliationStatus?)r.Status)
                .FirstOrDefault() == reconciliationStatus.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Never .Include()s Transactions just to count them (see docs/architecture.md's
        // Phase 6 "known tradeoff" note) — TransactionCount and the latest reconciliation
        // status are both plain SQL subqueries here, not materialized navigation collections.
        var rows = await query
            .OrderByDescending(s => s.UploadedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.Id,
                s.OriginalFileName,
                s.ProviderName,
                s.AccountHolderName,
                s.AccountNumberMasked,
                s.StatementPeriodStart,
                s.StatementPeriodEnd,
                TransactionCount = s.Transactions.Count,
                s.TotalDebits,
                s.TotalCredits,
                s.ProcessingStatus,
                LatestReconciliationStatus = s.ReconciliationResults
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => (ReconciliationStatus?)r.Status)
                    .FirstOrDefault(),
                s.UploadedAt
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new StatementSummaryResponse
        {
            Id = r.Id,
            OriginalFileName = r.OriginalFileName,
            ProviderName = r.ProviderName,
            AccountHolderName = r.AccountHolderName,
            AccountNumberMasked = r.AccountNumberMasked,
            StatementPeriodStart = r.StatementPeriodStart,
            StatementPeriodEnd = r.StatementPeriodEnd,
            TransactionCount = r.TransactionCount,
            TotalDebits = r.TotalDebits,
            TotalCredits = r.TotalCredits,
            ProcessingStatus = r.ProcessingStatus.ToString(),
            ReconciliationStatus = r.LatestReconciliationStatus?.ToString(),
            UploadedAt = r.UploadedAt
        }).ToList();

        return PagedResult<StatementSummaryResponse>.Create(items, totalCount, page, pageSize);
    }

    public async Task UpdateStatusAsync(
        Guid statementId,
        StatementProcessingStatus status,
        DateTime? processedAt,
        CancellationToken cancellationToken = default)
    {
        var statement = await dbContext.Statements.SingleAsync(s => s.Id == statementId, cancellationToken);
        statement.ProcessingStatus = status;
        if (processedAt.HasValue)
        {
            statement.ProcessedAt = processedAt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateExtractedFieldsAsync(Guid statementId, ExtractedStatementFields fields, CancellationToken cancellationToken = default)
    {
        var statement = await dbContext.Statements.SingleAsync(s => s.Id == statementId, cancellationToken);

        // Prefer whatever this extraction run found; if it found nothing for a field (e.g. this
        // run used OCR and OCR text is noisier than a prior direct-extraction pass), keep
        // whatever was already there rather than clobbering it with null.
        statement.AccountHolderName = fields.AccountHolderName ?? statement.AccountHolderName;
        statement.ProviderName = fields.ProviderName ?? statement.ProviderName;
        statement.AccountNumberMasked = fields.AccountNumberMasked ?? statement.AccountNumberMasked;
        statement.StatementPeriodStart = fields.StatementPeriodStart ?? statement.StatementPeriodStart;
        statement.StatementPeriodEnd = fields.StatementPeriodEnd ?? statement.StatementPeriodEnd;
        statement.StatementDate = fields.StatementDate ?? statement.StatementDate;
        statement.OpeningBalance = fields.OpeningBalance ?? statement.OpeningBalance;
        statement.ClosingBalance = fields.ClosingBalance ?? statement.ClosingBalance;
        statement.TotalDebits = fields.TotalDebits ?? statement.TotalDebits;
        statement.TotalCredits = fields.TotalCredits ?? statement.TotalCredits;
        statement.TotalPayments = fields.TotalPayments ?? statement.TotalPayments;
        statement.TotalPurchases = fields.TotalPurchases ?? statement.TotalPurchases;
        statement.Currency = fields.Currency ?? statement.Currency;
        statement.DocumentType = fields.DocumentType ?? statement.DocumentType;

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
