using FinancialStatementAI.Domain.Constants;
using FinancialStatementAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinancialStatementAI.Infrastructure.Persistence.Seed;

/// <summary>Ensures the system-defined default categories exist. Idempotent — safe to call on
/// every startup. Custom categories added later (see requirement #6: categories are
/// extensible) are untouched.</summary>
public static class CategorySeeder
{
    public static async Task SeedAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var existingNames = await dbContext.Categories
            .Select(c => c.Name)
            .ToListAsync(cancellationToken);

        var missingNames = DefaultCategories.Names.Except(existingNames, StringComparer.OrdinalIgnoreCase);

        foreach (var name in missingNames)
        {
            dbContext.Categories.Add(new Category
            {
                Name = name,
                IsSystemDefined = true,
                IsActive = true
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
