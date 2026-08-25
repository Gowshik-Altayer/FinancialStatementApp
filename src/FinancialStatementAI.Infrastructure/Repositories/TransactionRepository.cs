using FinancialStatementAI.Application.DTOs.Common;
using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Domain.Enums;
using FinancialStatementAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinancialStatementAI.Infrastructure.Repositories;

public class TransactionRepository(AppDbContext dbContext) : ITransactionRepository
{
    public async Task ReplaceForStatementAsync(
        Guid statementId,
        Guid userId,
        IEnumerable<Transaction> transactions,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.Transactions
            .Include(t => t.Extraction)
            .Where(t => t.StatementId == statementId)
            .ToListAsync(cancellationToken);

        // Match reparsed lines against the statement's own existing rows by natural key
        // (date + amount + description) and update in place rather than deleting and
        // recreating every row. A prior version of this method always deleted+recreated,
        // which cascade-deleted any TransactionClassification/TransactionCorrection history
        // (including human corrections) on every reprocess — see docs/ai-processing.md's
        // "known limitation" note. Preserving the row's identity here is what lets a human
        // category correction survive a reprocess: it's the same Transaction.Id, so its
        // Corrections are untouched, and TransactionClassificationService's "Known
        // Classification" rung finds that correction again by merchant text.
        var existingByKey = existing
            .GroupBy(NaturalKey)
            .ToDictionary(g => g.Key, g => new Queue<Transaction>(g));

        var newTransactions = transactions.ToList();
        var resultTransactions = new List<Transaction>(newTransactions.Count);
        var matchedIds = new HashSet<Guid>();

        foreach (var incoming in newTransactions)
        {
            if (existingByKey.TryGetValue(NaturalKey(incoming), out var queue) && queue.Count > 0)
            {
                var match = queue.Dequeue();
                matchedIds.Add(match.Id);
                ApplyReparsedFields(match, incoming);
                resultTransactions.Add(match);
            }
            else
            {
                dbContext.Transactions.Add(incoming);
                resultTransactions.Add(incoming);
            }
        }

        // Anything left over no longer appears in the fresh parse (e.g. the statement text
        // changed) — its history is no longer meaningful without a matching row, so it's
        // removed along with its (cascade-deleted) classifications/corrections.
        var stale = existing.Where(t => !matchedIds.Contains(t.Id));
        dbContext.Transactions.RemoveRange(stale);

        // Duplicate detection (requirement #21) against the same user's OTHER statements —
        // never this statement's own transactions, since re-running this statement's own parse
        // (reprocess) must not flag a statement as a duplicate of itself.
        var otherStatementIds = await dbContext.Statements
            .Where(s => s.UserId == userId && s.Id != statementId)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var candidates = otherStatementIds.Count == 0
            ? []
            : await dbContext.Transactions
                .Where(t => otherStatementIds.Contains(t.StatementId))
                .Select(t => new { t.Id, t.TransactionDate, t.Amount, t.Merchant })
                .ToListAsync(cancellationToken);

        foreach (var transaction in resultTransactions)
        {
            var duplicate = candidates.FirstOrDefault(c =>
                c.TransactionDate == transaction.TransactionDate &&
                c.Amount == transaction.Amount &&
                string.Equals(c.Merchant, transaction.Merchant, StringComparison.OrdinalIgnoreCase));

            transaction.IsPotentialDuplicate = duplicate is not null;
            transaction.DuplicateOfTransactionId = duplicate?.Id;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static (DateOnly? Date, decimal? Amount, string Description) NaturalKey(Transaction transaction) =>
        (transaction.TransactionDate, transaction.Amount, transaction.Description.Trim().ToUpperInvariant());

    /// <summary>Copies every field a fresh reparse can legitimately change onto an existing,
    /// identity-preserved row. Deliberately excludes <see cref="Transaction.CategoryId"/> —
    /// classification runs as its own step right after this one, and a human's prior category
    /// correction must not be silently reset by a bare re-extraction in between.</summary>
    private static void ApplyReparsedFields(Transaction existingTransaction, Transaction incoming)
    {
        existingTransaction.PostingDate = incoming.PostingDate;
        existingTransaction.Merchant = incoming.Merchant;
        existingTransaction.ReferenceNumber = incoming.ReferenceNumber;
        existingTransaction.DebitAmount = incoming.DebitAmount;
        existingTransaction.CreditAmount = incoming.CreditAmount;
        existingTransaction.Currency = incoming.Currency;
        existingTransaction.TransactionType = incoming.TransactionType;
        existingTransaction.PageSourceLocation = incoming.PageSourceLocation;

        if (existingTransaction.Extraction is not null && incoming.Extraction is not null)
        {
            existingTransaction.Extraction.RawText = incoming.Extraction.RawText;
            existingTransaction.Extraction.ExtractionMethod = incoming.Extraction.ExtractionMethod;
        }
        else if (incoming.Extraction is not null)
        {
            existingTransaction.Extraction = incoming.Extraction;
        }
    }

    public async Task<IReadOnlyList<Transaction>> GetByStatementIdAsync(Guid statementId, CancellationToken cancellationToken = default) =>
        await dbContext.Transactions
            .Include(t => t.Category)
            .Include(t => t.Classifications)
            .Include(t => t.Corrections)
            .Where(t => t.StatementId == statementId)
            .OrderBy(t => t.TransactionDate)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task<Transaction?> GetByIdAsync(Guid transactionId, CancellationToken cancellationToken = default) =>
        dbContext.Transactions
            .Include(t => t.Statement)
            .Include(t => t.Category)
            .Include(t => t.Classifications)
            .Include(t => t.Corrections).ThenInclude(c => c.CorrectedByUser)
            .AsSplitQuery()
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == transactionId, cancellationToken);

    public async Task<IReadOnlyList<Transaction>> GetReviewQueueAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await dbContext.Transactions
            .Include(t => t.Statement)
            .Include(t => t.Category)
            .Include(t => t.Classifications)
            .Include(t => t.Corrections)
            .Where(t => t.Statement!.UserId == userId && t.Statement.ProcessingStatus == StatementProcessingStatus.PendingReview)
            .OrderBy(t => t.Classifications.Where(c => c.IsCurrent).Select(c => c.ConfidenceScore).FirstOrDefault())
            .ThenByDescending(t => t.Statement!.UploadedAt)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task ApplyCorrectionAsync(Guid transactionId, Guid categoryId, TransactionCorrection correction, CancellationToken cancellationToken = default)
    {
        var transaction = await dbContext.Transactions.SingleAsync(t => t.Id == transactionId, cancellationToken);
        transaction.CategoryId = categoryId;

        dbContext.TransactionCorrections.Add(correction);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ApplyClassificationAsync(
        Guid transactionId,
        Guid categoryId,
        decimal confidenceScore,
        ClassificationMethod method,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var previousCurrent = await dbContext.TransactionClassifications
            .Where(c => c.TransactionId == transactionId && c.IsCurrent)
            .ToListAsync(cancellationToken);
        foreach (var previous in previousCurrent)
        {
            previous.IsCurrent = false;
        }

        dbContext.TransactionClassifications.Add(new TransactionClassification
        {
            TransactionId = transactionId,
            CategoryId = categoryId,
            ConfidenceScore = confidenceScore,
            ClassificationMethod = method,
            Reason = reason,
            IsCurrent = true
        });

        var transaction = await dbContext.Transactions.SingleAsync(t => t.Id == transactionId, cancellationToken);
        transaction.CategoryId = categoryId;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<Transaction>> SearchAsync(
        Guid userId,
        string? search,
        Guid? categoryId,
        Guid? statementId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Transactions.Where(t => t.Statement!.UserId == userId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(t =>
                t.Description.ToLower().Contains(term) ||
                (t.Merchant != null && t.Merchant.ToLower().Contains(term)));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(t => t.CategoryId == categoryId.Value);
        }

        if (statementId.HasValue)
        {
            query = query.Where(t => t.StatementId == statementId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Page over Ids first (a lean query) rather than paginating the fully-hydrated,
        // multi-Include entity query directly — Skip/Take alongside several one-to-many
        // Includes is exactly the shape that risks duplicated/incorrect paging in EF Core.
        var ids = await query
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        var hydrated = await dbContext.Transactions
            .Include(t => t.Statement)
            .Include(t => t.Category)
            .Include(t => t.Classifications)
            .Include(t => t.Corrections)
            .Where(t => ids.Contains(t.Id))
            .AsSplitQuery()
            .AsNoTracking()
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        // Re-apply the id query's order — the hydration query above has none of its own.
        var items = ids.Select(id => hydrated[id]).ToList();

        return PagedResult<Transaction>.Create(items, totalCount, page, pageSize);
    }
}
