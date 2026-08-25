using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Application.Interfaces;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace FinancialStatementAI.Infrastructure.AI.Classification;

/// <summary>Real classification via the public OpenAI API. Requires "OpenAI:ApiKey" (via User
/// Secrets/environment — never appsettings.json). Selected instead of
/// <see cref="MockTransactionClassifier"/> by setting "Classification:Provider" to "OpenAI".</summary>
public class OpenAiTransactionClassifier : ITransactionClassifier
{
    private readonly ChatClient _chatClient;

    public OpenAiTransactionClassifier(IOptions<OpenAiOptions> options)
    {
        var settings = options.Value;
        _chatClient = new OpenAIClient(settings.ApiKey).GetChatClient(settings.Model);
    }

    public Task<ClassificationResult> ClassifyAsync(
        string merchantOrDescription,
        decimal? amount,
        IReadOnlyList<string> validCategoryNames,
        CancellationToken cancellationToken = default) =>
        ChatCompletionClassifierCore.ClassifyAsync(_chatClient, merchantOrDescription, amount, validCategoryNames, cancellationToken);
}
