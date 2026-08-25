using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinancialStatementAI.Infrastructure.Repositories;

public class CategoryRepository(AppDbContext dbContext) : ICategoryRepository
{
    public async Task<IReadOnlyList<Category>> GetAllActiveAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Categories.Where(c => c.IsActive).AsNoTracking().ToListAsync(cancellationToken);

    public Task<Category?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
        dbContext.Categories.AsNoTracking().SingleOrDefaultAsync(c => c.Name == name, cancellationToken);
}
