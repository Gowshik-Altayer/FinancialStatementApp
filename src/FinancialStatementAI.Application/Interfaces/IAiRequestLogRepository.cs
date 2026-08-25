using FinancialStatementAI.Domain.Entities;

namespace FinancialStatementAI.Application.Interfaces;

/// <summary>Persists one row per LLM call for cost/usage tracking (requirement #46) — never
/// called for Rule/MerchantMapping/PreviousCorrection hits, since those don't call an LLM.</summary>
public interface IAiRequestLogRepository
{
    Task AddAsync(AIRequest request, CancellationToken cancellationToken = default);
}
