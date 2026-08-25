using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Application.Interfaces;

namespace FinancialStatementAI.Infrastructure.AI.Classification;

/// <summary>Development/demo default — deliberately honest rather than falsely confident: with
/// no real LLM configured, it always returns "Other" at a low (review-triggering) confidence
/// rather than guessing, since a wrong guess presented confidently is worse than an honest "we
/// don't know, please review" for a merchant Rules/Merchant Mapping/prior corrections couldn't
/// place. Set "Classification:Provider" to "OpenAI" or "AzureOpenAI" for real LLM classification.</summary>
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
