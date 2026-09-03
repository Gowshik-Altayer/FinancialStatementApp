using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace FinancialStatementAI.Infrastructure.AI.Classification;

/// <summary>Loads (downloading once if not already cached) a small open-weight sentence-embedding
/// model — all-MiniLM-L6-v2 (Apache 2.0, ~23MB quantized ONNX export) — and turns text into a
/// 384-dim, L2-normalized embedding. This is the zero-cost, zero-account "Embeddings" option from
/// the challenge's Requirement #6 category-classification list: no API key, no per-call cost, no
/// external service once the model file is on disk, since the model runs entirely in-process via
/// ONNX Runtime.
///
/// The model/tokenizer are a process-wide singleton (a static <see cref="Lazy{T}"/>, not per-DI-
/// scope state): <see cref="EmbeddingTransactionClassifier"/> is registered Scoped like its
/// OpenAI/Claude/Ollama siblings, but reloading a 23MB ONNX session and re-downloading the model
/// on every statement's classification run (once per DI scope) would defeat the point of caching
/// it at all.</summary>
internal static class LocalEmbeddingModel
{
    private const string ModelUrl = "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model_quint8_avx2.onnx";
    private const string VocabUrl = "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/vocab.txt";
    private const int HiddenSize = 384;

    private static Lazy<Task<(InferenceSession Session, BertTokenizer Tokenizer)>>? _modelLazy;
    private static readonly object InitLock = new();

    public static Task<(InferenceSession Session, BertTokenizer Tokenizer)> GetModelAsync(string modelCacheDirectory, CancellationToken cancellationToken)
    {
        if (_modelLazy is null)
        {
            lock (InitLock)
            {
                _modelLazy ??= new Lazy<Task<(InferenceSession, BertTokenizer)>>(() => LoadModelAsync(modelCacheDirectory, cancellationToken));
            }
        }

        return _modelLazy.Value;
    }

    private static async Task<(InferenceSession, BertTokenizer)> LoadModelAsync(string modelCacheDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(modelCacheDirectory);
        var modelPath = Path.Combine(modelCacheDirectory, "model.onnx");
        var vocabPath = Path.Combine(modelCacheDirectory, "vocab.txt");

        using var http = new HttpClient();
        if (!File.Exists(modelPath))
        {
            await DownloadAsync(http, ModelUrl, modelPath, cancellationToken);
        }

        if (!File.Exists(vocabPath))
        {
            await DownloadAsync(http, VocabUrl, vocabPath, cancellationToken);
        }

        var tokenizer = BertTokenizer.Create(vocabPath);
        var session = new InferenceSession(modelPath);
        return (session, tokenizer);
    }

    // Downloads to a ".download" temp file first and renames on success, so a connection dropped
    // mid-download never leaves a truncated file behind masquerading as a complete, cached one.
    private static async Task DownloadAsync(HttpClient http, string url, string destinationPath, CancellationToken cancellationToken)
    {
        var tempPath = destinationPath + ".download";
        await using (var stream = await http.GetStreamAsync(url, cancellationToken))
        await using (var file = File.Create(tempPath))
        {
            await stream.CopyToAsync(file, cancellationToken);
        }

        File.Move(tempPath, destinationPath, overwrite: true);
    }

    /// <summary>Standard sentence-transformers pooling recipe: mean-pool the token-level
    /// last_hidden_state over the sequence dimension (weighted by attention_mask, so any future
    /// padding is excluded), then L2-normalize — after which cosine similarity between two
    /// embeddings reduces to a plain dot product (see EmbeddingTransactionClassifier).</summary>
    public static float[] Embed(InferenceSession session, BertTokenizer tokenizer, string text)
    {
        var ids = tokenizer.EncodeToIds(text).ToArray();
        var seqLen = ids.Length;
        var idsAsLong = new long[seqLen];
        var mask = new long[seqLen];
        for (var i = 0; i < seqLen; i++)
        {
            idsAsLong[i] = ids[i];
            mask[i] = 1;
        }

        var tokenTypeIds = new long[seqLen];

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(idsAsLong, [1, seqLen])),
            NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(mask, [1, seqLen])),
            NamedOnnxValue.CreateFromTensor("token_type_ids", new DenseTensor<long>(tokenTypeIds, [1, seqLen]))
        };

        using var results = session.Run(inputs);
        var lastHiddenState = results.First(r => r.Name == "last_hidden_state").AsTensor<float>();

        var pooled = new float[HiddenSize];
        for (var i = 0; i < seqLen; i++)
        {
            for (var j = 0; j < HiddenSize; j++)
            {
                pooled[j] += lastHiddenState[0, i, j] * mask[i];
            }
        }

        var maskSum = (float)mask.Sum();
        for (var j = 0; j < HiddenSize; j++)
        {
            pooled[j] /= Math.Max(maskSum, 1e-9f);
        }

        var norm = MathF.Sqrt(pooled.Sum(x => x * x));
        for (var j = 0; j < HiddenSize; j++)
        {
            pooled[j] /= Math.Max(norm, 1e-9f);
        }

        return pooled;
    }
}
