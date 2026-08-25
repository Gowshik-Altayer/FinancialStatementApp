namespace FinancialStatementAI.Application.Interfaces;

/// <summary>The "Known Classification" rung of the hybrid ladder (requirement #17) — have we
/// already seen a human correct this exact merchant to a specific category before? This is how
/// human corrections improve future classification (requirement #9's reasoning question #10)
/// without any retraining: a corrected category for "ACME SERVICES" today means the next "ACME
/// SERVICES" transaction, on any statement, is classified from that correction with high
/// confidence instead of falling through to the LLM again.</summary>
public interface IClassificationHistoryRepository
{
    Task<string?> FindPreviousCorrectedCategoryAsync(Guid userId, string merchant, CancellationToken cancellationToken = default);
}
