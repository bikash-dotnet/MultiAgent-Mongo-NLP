namespace Gateway.Nlp;

public sealed class NlpOptions
{
    public const string SectionName = "Nlp";

    public EmbeddingsOptions Embeddings { get; set; } = new();
    public CacheOptions Cache { get; set; } = new();
    public DefaultsOptions Defaults { get; set; } = new();

    public sealed class EmbeddingsOptions
    {
        public string ModelPath { get; set; } = "Models/bge-small-en-v1.5/model_quantized.onnx";
        public string TokenizerPath { get; set; } = "Models/bge-small-en-v1.5/vocab.txt";
        public int MaxTokens { get; set; } = 512;
    }

    public sealed class CacheOptions
    {
        public double Threshold { get; set; } = 0.95;
    }

    public sealed class DefaultsOptions
    {
        public int Limit { get; set; } = 10;
        public string Sort { get; set; } = "rating_desc";
        public string Market { get; set; } = "All";
    }
}
