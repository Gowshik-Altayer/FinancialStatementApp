using System.Text.Json;
using FinancialStatementAI.Application.DTOs.Statements;
using OpenAI.Chat;

namespace FinancialStatementAI.Infrastructure.AI.Classification;

/// <summary>Shared prompt + structured-JSON-output parsing (requirement #15) used by both
/// OpenAiTransactionClassifier and AzureOpenAiTransactionClassifier — Azure OpenAI's client (in
/// the 2.x SDK generation) exposes the same <see cref="ChatClient"/> type as the plain OpenAI
/// client, so the two only differ in how that client is constructed.</summary>
internal static class ChatCompletionClassifierCore
{
    public static async Task<ClassificationResult> ClassifyAsync(
        ChatClient chatClient,
        string merchantOrDescription,
        decimal? amount,
        IReadOnlyList<string> validCategoryNames,
        CancellationToken cancellationToken)
    {
        const string jsonShape =
            "{\"category\": \"<one of the categories above, exactly as written>\", " +
            "\"confidence\": <number between 0 and 1>, \"reason\": \"<brief one-sentence reason>\"}";

        var prompt =
            $"""
             Classify the following bank/credit-card transaction into exactly one of these categories: {string.Join(", ", validCategoryNames)}.

             Transaction description/merchant: "{merchantOrDescription}"
             {(amount is not null ? $"Amount: {amount}" : "")}

             Respond with ONLY a JSON object in this exact shape, no other text:
             {jsonShape}

             Only use information present in the transaction description above - never invent a date, amount, or reference number.
             If you are not confident which category applies, use category "Other" with a low confidence value rather than guessing.
             """;

        try
        {
            var completion = await chatClient.CompleteChatAsync(
                [new UserChatMessage(prompt)],
                new ChatCompletionOptions { ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat() },
                cancellationToken);

            var json = completion.Value.Content.Count > 0 ? completion.Value.Content[0].Text : null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return ClassificationResult.Failure("LLM returned an empty response.");
            }

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var category = root.TryGetProperty("category", out var categoryElement) ? categoryElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(category))
            {
                return ClassificationResult.Failure("LLM response did not include a category.");
            }

            var confidence = root.TryGetProperty("confidence", out var confidenceElement) && confidenceElement.TryGetDecimal(out var value)
                ? value
                : 0.5m;
            var reason = root.TryGetProperty("reason", out var reasonElement) ? reasonElement.GetString() : null;

            return ClassificationResult.Success(category, Math.Clamp(confidence, 0m, 1m), reason ?? string.Empty);
        }
        catch (Exception ex)
        {
            // Broad catch is deliberate: an API failure, timeout, or malformed JSON response
            // must degrade this one transaction to "needs review", not crash the whole
            // statement's processing (requirement #14).
            return ClassificationResult.Failure($"LLM classification request failed: {ex.Message}");
        }
    }
}
