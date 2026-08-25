using Azure;
using Azure.AI.OpenAI;
using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Application.Interfaces;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace FinancialStatementAI.Infrastructure.AI.Classification;

/// <summary>Real classification via an Azure OpenAI deployment. Requires
/// "Azure:OpenAI:Endpoint", "Azure:OpenAI:ApiKey" (via User Secrets/environment), and
/// "Azure:OpenAI:DeploymentName". Selected instead of <see cref="MockTransactionClassifier"/> by
/// setting "Classification:Provider" to "AzureOpenAI".</summary>
public class AzureOpenAiTransactionClassifier : ITransactionClassifier
{
    private readonly ChatClient _chatClient;

    public AzureOpenAiTransactionClassifier(IOptions<AzureOpenAiOptions> options)
    {
        var settings = options.Value;
        var client = new AzureOpenAIClient(new Uri(settings.Endpoint), new AzureKeyCredential(settings.ApiKey));
        _chatClient = client.GetChatClient(settings.DeploymentName);
    }

    public Task<ClassificationResult> ClassifyAsync(
        string merchantOrDescription,
        decimal? amount,
        IReadOnlyList<string> validCategoryNames,
        CancellationToken cancellationToken = default) =>
        ChatCompletionClassifierCore.ClassifyAsync(_chatClient, merchantOrDescription, amount, validCategoryNames, cancellationToken);
}
