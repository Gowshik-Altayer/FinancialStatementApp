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

    public async Task<IReadOnlyList<Statement>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await dbContext.Statements
            .Include(s => s.Transactions)
            .Include(s => s.StatementExtraction)
            .Include(s => s.ReconciliationResults)
            .AsSplitQuery()
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .ToListAsync(cancellationToken);

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

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
