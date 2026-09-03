using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Constants;
using Microsoft.Extensions.Options;

namespace FinancialStatementAI.Infrastructure.AI.Classification;

/// <summary>Real classification with zero API cost and zero external service: embeds the
/// transaction text with a small local model (see <see cref="LocalEmbeddingModel"/>) and finds the
/// nearest known example by cosine similarity, rather than looking an LLM up over the network.
/// Unlike MerchantMappingRepository's substring match (Rung 2 of the classification ladder), this
/// generalizes semantically — "AWS EMEA" lands close to the seeded "AWS" example even though
/// neither string contains the other. Selected instead of
/// <see cref="MockTransactionClassifier"/> by setting "Classification:Provider" to
/// "Embeddings".</summary>
public class EmbeddingTransactionClassifier : ITransactionClassifier
{
    private readonly EmbeddingOptions _options;

    // The corpus is the same seed data MerchantMappingRepository uses (DefaultMerchantMappings —
    // the challenge's own worked examples plus other common, unambiguous merchants), reused here
    // rather than duplicated, so the two rungs stay in sync if the seed list changes.
    private static readonly (string Text, string CategoryName)[] Corpus =
        DefaultMerchantMappings.Mappings.Select(m => (m.Pattern, m.CategoryName)).ToArray();

    private static readonly object CorpusEmbeddingsLock = new();
    private static float[][]? _corpusEmbeddings;

    public EmbeddingTransactionClassifier(IOptions<EmbeddingOptions> options)
    {
        _options = options.Value;
    }

    public async Task<ClassificationResult> ClassifyAsync(
        string merchantOrDescription,
        decimal? amount,
        IReadOnlyList<string> validCategoryNames,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (session, tokenizer) = await LocalEmbeddingModel.GetModelAsync(_options.ModelCacheDirectory, cancellationToken);

            float[][] corpusEmbeddings;
            lock (CorpusEmbeddingsLock)
            {
                _corpusEmbeddings ??= Corpus.Select(c => LocalEmbeddingModel.Embed(session, tokenizer, c.Text)).ToArray();
                corpusEmbeddings = _corpusEmbeddings;
            }

            var queryEmbedding = LocalEmbeddingModel.Embed(session, tokenizer, merchantOrDescription);

            var bestIndex = -1;
            var bestSimilarity = float.MinValue;
            for (var i = 0; i < corpusEmbeddings.Length; i++)
            {
                var similarity = CosineSimilarity(queryEmbedding, corpusEmbeddings[i]);
                if (similarity > bestSimilarity)
                {
                    bestSimilarity = similarity;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0 || bestSimilarity < _options.MinimumSimilarity)
            {
                return ClassificationResult.Success(
                    "Other", 0.30m,
                    $"No known example was similar enough (best cosine similarity {bestSimilarity:F2}) — classified as Other rather than guessing.");
            }

            var (matchedText, categoryName) = Corpus[bestIndex];
            if (!validCategoryNames.Contains(categoryName, StringComparer.OrdinalIgnoreCase))
            {
                // The matched example's category isn't in this user's active category list (e.g.
                // deactivated) — don't return a category the caller can't resolve.
                return ClassificationResult.Success("Other", 0.30m, $"Matched example's category \"{categoryName}\" is no longer active.");
            }

            // Linearly maps [MinimumSimilarity, 1.0] -> [0.55, 0.95]: a borderline match lands
            // just above the "review recommended" threshold, a near-exact match lands close to
            // (but never above) Rules/Merchant Mapping's own hand-set 0.90-0.95 confidence.
            var similarityRange = 1.0f - _options.MinimumSimilarity;
            var scaledConfidence = 0.55m + (decimal)((bestSimilarity - _options.MinimumSimilarity) / similarityRange) * 0.40m;

            return ClassificationResult.Success(
                categoryName,
                Math.Clamp(scaledConfidence, 0m, 1m),
                $"Closest known example was \"{matchedText}\" (cosine similarity {bestSimilarity:F2}).");
        }
        catch (Exception ex)
        {
            // Broad catch is deliberate: a first-run download failure, a corrupted cached model
            // file, or an inference error must degrade this one transaction to "needs review," not
            // crash the whole statement's processing (requirement #14).
            return ClassificationResult.Failure($"Local embedding classification failed: {ex.Message}");
        }
    }

    // Both vectors are already L2-normalized (see LocalEmbeddingModel.Embed), so cosine similarity
    // reduces to a plain dot product.
    private static float CosineSimilarity(float[] a, float[] b)
    {
        float sum = 0;
        for (var i = 0; i < a.Length; i++)
        {
            sum += a[i] * b[i];
        }

        return sum;
    }
}
