using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Application.Interfaces;

namespace FinancialStatementAI.Infrastructure.AI.Classification;

/// <summary>Explicit opt-in only (set "Classification:Provider" to "Mock") — not the default.
/// Deliberately honest rather than falsely confident: it always returns "Other" at a low (review-
/// triggering) confidence rather than guessing, since a wrong guess presented confidently is worse
/// than an honest "we don't know, please review" for a merchant Rules/Merchant Mapping/prior
/// corrections couldn't place. Useful for fast, fully offline/deterministic tests where even the
/// zero-cost <see cref="EmbeddingTransactionClassifier"/> default (a real, if small, model
/// inference) is more than a test needs. For real classification, the default already handles
/// it — see DependencyInjection's fallback case — or set the provider to "OpenAI"/"AzureOpenAI"
/// for a hosted LLM instead.</summary>
public class MockTransactionClassifier : ITransactionClassifier
{
    public Task<ClassificationResult> ClassifyAsync(
        string merchantOrDescription,
        decimal? amount,
        IReadOnlyList<string> validCategoryNames,
        CancellationToken cancellationToken = default)
    {
        var category = validCategoryNames.FirstOrDefault(c => c.Equals("Other", StringComparison.OrdinalIgnoreCase))
            ?? validCategoryNames.FirstOrDefault() ?? "Other";

        return Task.FromResult(ClassificationResult.Success(
            category,
            confidence: 0.50m,
            reason: "MOCK classifier - no real LLM configured; unable to confidently classify this merchant."));
    }
}
