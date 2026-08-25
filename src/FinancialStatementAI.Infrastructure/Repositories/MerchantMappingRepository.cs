using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Domain.Enums;
using FinancialStatementAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinancialStatementAI.Infrastructure.Repositories;

public class MerchantMappingRepository(AppDbContext dbContext) : IMerchantMappingRepository
{
    public async Task<MerchantMapping?> FindMatchAsync(string merchantOrDescription, CancellationToken cancellationToken = default)
    {
        // Loaded client-side: the mapping table is small (tens to low hundreds of rows) and the
        // match logic (Contains/StartsWith/Exact, case-insensitive) doesn't translate cleanly to
        // SQL across all three modes in one query.
        var mappings = await dbContext.MerchantMappings
            .Include(m => m.Category)
            .Where(m => m.IsActive)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return mappings.FirstOrDefault(m => Matches(m, merchantOrDescription));
    }

    private static bool Matches(MerchantMapping mapping, string text) => mapping.MatchType switch
    {
        MerchantMatchType.Exact => text.Equals(mapping.MerchantPattern, StringComparison.OrdinalIgnoreCase),
        MerchantMatchType.StartsWith => text.StartsWith(mapping.MerchantPattern, StringComparison.OrdinalIgnoreCase),
        _ => text.Contains(mapping.MerchantPattern, StringComparison.OrdinalIgnoreCase)
    };
}
