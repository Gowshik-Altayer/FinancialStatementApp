using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Entities;
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
            .AsSplitQuery()
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Statement>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await dbContext.Statements
            .Include(s => s.Transactions)
            .AsSplitQuery()
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .ToListAsync(cancellationToken);
}
