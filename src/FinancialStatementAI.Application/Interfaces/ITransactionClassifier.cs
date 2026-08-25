using FinancialStatementAI.Application.DTOs.Statements;

namespace FinancialStatementAI.Application.Interfaces;

/// <summary>The LLM rung of the hybrid classification ladder (requirement #17) — only called for
/// transactions Rules, Merchant Mapping, and prior human corrections couldn't confidently
/// classify (requirement #46: don't send every transaction to the LLM). Implementations must
/// return one of the challenge's fixed category names or fail — never an invented category
/// (requirement #15: validate AI output, don't trust it blindly).</summary>
public interface ITransactionClassifier
{
    Task<ClassificationResult> ClassifyAsync(
        string merchantOrDescription,
        decimal? amount,
        IReadOnlyList<string> validCategoryNames,
        CancellationToken cancellationToken = default);
}
