using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Constants;
using FinancialStatementAI.Domain.Entities;
using FinancialStatementAI.Domain.Enums;

namespace FinancialStatementAI.Application.Services;

/// <summary>Orchestrates the hybrid classification ladder (requirement #17): Rules -> Merchant
/// Mapping -> Known Classification (prior human corrections) -> LLM. Stops at the first rung that
/// confidently matches — the LLM is only ever called when nothing else could classify the
/// transaction (requirement #46: don't send every transaction to the LLM).</summary>
public class TransactionClassificationService(
    ITransactionRepository transactionRepository,
    ICategoryRepository categoryRepository,
    IMerchantMappingRepository merchantMappingRepository,
    IClassificationHistoryRepository classificationHistoryRepository,
    ITransactionClassifier transactionClassifier,
    IAiRequestLogRepository aiRequestLogRepository) : ITransactionClassificationService
{
    public async Task ClassifyStatementTransactionsAsync(Guid statementId, Guid userId, CancellationToken cancellationToken = default)
    {
        var transactions = await transactionRepository.GetByStatementIdAsync(statementId, cancellationToken);
        if (transactions.Count == 0)
        {
            return;
        }

        var categories = await categoryRepository.GetAllActiveAsync(cancellationToken);
        var categoryNames = categories.Select(c => c.Name).ToList();
        var otherCategory = categories.FirstOrDefault(c => c.Name.Equals("Other", StringComparison.OrdinalIgnoreCase));

        foreach (var transaction in transactions)
        {
            var text = transaction.Merchant ?? transaction.Description;
            var (categoryName, confidence, method, reason) = await ClassifyOneAsync(transaction, text, userId, categoryNames, cancellationToken);

            var category = categories.FirstOrDefault(c => c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase))
                ?? otherCategory;

            if (category is null)
            {
                continue; // no "Other" category seeded — nothing sensible to record
            }

            // Requirement #15: never trust arbitrary AI output. If the classifier (LLM) returned
            // a category name that isn't one of ours, fall back to Other and say so explicitly
            // rather than silently accepting an invented category.
            if (!category.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase))
            {
                reason = $"Classifier returned an unrecognized category ('{categoryName}'); defaulted to Other. Original reason: {reason}";
                confidence = Math.Min(confidence, ClassificationConfidenceThresholds.ReviewRecommendedMinimum - 0.01m);
            }

            await transactionRepository.ApplyClassificationAsync(
                transaction.Id, category.Id, Math.Clamp(confidence, 0m, 1m), method, reason, cancellationToken);
        }
    }

    private async Task<(string CategoryName, decimal Confidence, ClassificationMethod Method, string? Reason)> ClassifyOneAsync(
        Transaction transaction, string text, Guid userId, IReadOnlyList<string> categoryNames, CancellationToken cancellationToken)
    {
        // Checked against the full Description, not just Merchant/text — these are structural
        // signals ("PAYROLL", "RENT PAYMENT") that can appear in the description even when the
        // merchant name itself doesn't carry them.
        var keywordRule = ClassificationKeywordRules.Rules
            .FirstOrDefault(r => transaction.Description.Contains(r.Keyword, StringComparison.OrdinalIgnoreCase));
        if (keywordRule != default)
        {
            return (keywordRule.CategoryName, 0.95m, ClassificationMethod.Rule, $"Matched rule keyword \"{keywordRule.Keyword}\".");
        }

        var merchantMapping = await merchantMappingRepository.FindMatchAsync(text, cancellationToken);
        if (merchantMapping is not null)
        {
            return (merchantMapping.Category!.Name, 0.90m, ClassificationMethod.MerchantMapping,
                $"Matched known merchant pattern \"{merchantMapping.MerchantPattern}\".");
        }

        if (!string.IsNullOrWhiteSpace(transaction.Merchant))
        {
            var previousCategory = await classificationHistoryRepository.FindPreviousCorrectedCategoryAsync(
                userId, transaction.Merchant, cancellationToken);
            if (previousCategory is not null)
            {
                return (previousCategory, 0.95m, ClassificationMethod.PreviousCorrection,
                    $"A human previously corrected \"{transaction.Merchant}\" to this category.");
            }
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var llmResult = await transactionClassifier.ClassifyAsync(text, transaction.Amount, categoryNames, cancellationToken);
        stopwatch.Stop();

        await aiRequestLogRepository.AddAsync(new AIRequest
        {
            TransactionId = transaction.Id,
            StatementId = transaction.StatementId,
            Provider = transactionClassifier.GetType().Name,
            Model = "n/a",
            RequestType = "Classification",
            DurationMs = (int)stopwatch.ElapsedMilliseconds,
            IsSuccess = llmResult.IsSuccess,
            ErrorMessage = llmResult.ErrorMessage
        }, cancellationToken);

        if (llmResult.IsSuccess && llmResult.CategoryName is not null)
        {
            return (llmResult.CategoryName, llmResult.Confidence, ClassificationMethod.Llm, llmResult.Reason);
        }

        return ("Other", 0m, ClassificationMethod.Llm, $"Classification failed: {llmResult.ErrorMessage}");
    }
}
