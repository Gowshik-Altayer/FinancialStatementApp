using FinancialStatementAI.Domain.Constants;
using FinancialStatementAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinancialStatementAI.Infrastructure.Persistence.Seed;

/// <summary>Idempotent, like CategorySeeder — must run after it, since mappings reference
/// categories by name.</summary>
public static class MerchantMappingSeeder
{
    public static async Task SeedAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var existingPatterns = await dbContext.MerchantMappings
            .Select(m => m.MerchantPattern)
            .ToListAsync(cancellationToken);

        var categoriesByName = await dbContext.Categories
            .ToDictionaryAsync(c => c.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var (pattern, categoryName) in DefaultMerchantMappings.Mappings)
        {
            if (existingPatterns.Contains(pattern, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!categoriesByName.TryGetValue(categoryName, out var category))
            {
                continue; // category not seeded (shouldn't happen if CategorySeeder ran first)
            }

            dbContext.MerchantMappings.Add(new MerchantMapping
            {
                MerchantPattern = pattern,
                CategoryId = category.Id,
                IsSystemDefined = true,
                IsActive = true
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
