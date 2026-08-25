using FinancialStatementAI.Domain.Entities;

namespace FinancialStatementAI.Application.Interfaces;

public interface IMerchantMappingRepository
{
    /// <summary>Returns the first active mapping whose pattern matches
    /// <paramref name="merchantOrDescription"/>, or null if none do.</summary>
    Task<MerchantMapping?> FindMatchAsync(string merchantOrDescription, CancellationToken cancellationToken = default);
}
