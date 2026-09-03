namespace FinancialStatementAI.Infrastructure.AI.Classification;

public class EmbeddingOptions
{
    public const string SectionName = "Embeddings";

    // Where the downloaded model + tokenizer vocab are cached, relative to the process's working
    // directory — matches LocalFileStorageOptions.RootPath's own "App_Data/..." convention.
    // Downloaded once on first use (see LocalEmbeddingModel), reused on every run after that.
    public string ModelCacheDirectory { get; set; } = "App_Data/models/all-MiniLM-L6-v2";

    // Below this cosine similarity, no corpus example counts as a real match — same "honest low
    // confidence over false confidence" philosophy as MockTransactionClassifier. Calibrated
    // empirically against real merchant strings: near-duplicate strings ("UBER *TRIP 8827" vs
    // "UBER TRIP") score ~0.7-0.8, genuinely unrelated merchants score ~0.15-0.25, and
    // semantically-related-but-differently-worded merchants ("AWS EMEA" vs "AMAZON WEB SERVICES",
    // "SQ *BLUE BOTTLE COFFEE" vs "STARBUCKS COFFEE") score ~0.44-0.59.
    public float MinimumSimilarity { get; set; } = 0.40f;
}
