using Gateway.Nlp.Abstractions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace Gateway.Nlp.Embeddings;

public static class EmbedderDimensions
{
    public const int Dimension = 384;
}

public sealed class OnnxBgeSmallEmbedder : ITextEmbedder, IDisposable
{
    public const string Instruction = "Represent this sentence for searching relevant passages: ";

    private readonly InferenceSession _session;
    private readonly BertTokenizer _tokenizer;
    private readonly int _maxTokens;

    public OnnxBgeSmallEmbedder(string modelPath, string vocabPath, int maxTokens = 512)
    {
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException(
                $"ONNX model not found at '{modelPath}'. Run scripts/download-nlp-assets.sh first.", modelPath);
        }

        if (!File.Exists(vocabPath))
        {
            throw new FileNotFoundException(
                $"Tokenizer vocab not found at '{vocabPath}'. Run scripts/download-nlp-assets.sh first.", vocabPath);
        }

        _session = new InferenceSession(modelPath);
        _tokenizer = BertTokenizer.Create(vocabPath);
        _maxTokens = maxTokens;
    }

    public float[] Embed(string text)
    {
        var tokens = _tokenizer.EncodeToIds(Instruction + text);
        var count = Math.Min(tokens.Count, _maxTokens);

        var ids = new long[count];
        for (var i = 0; i < count; i++) ids[i] = tokens[i];

        var mask = new long[count];
        var types = new long[count];
        Array.Fill(mask, 1L);

        var inputIds = new DenseTensor<long>(ids, new[] { 1, count });
        var attention = new DenseTensor<long>(mask, new[] { 1, count });
        var typeIds = new DenseTensor<long>(types, new[] { 1, count });

        using var results = _session.Run(new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attention),
            NamedOnnxValue.CreateFromTensor("token_type_ids", typeIds)
        });

        var output = results.First(r => r.Name == "last_hidden_state").AsTensor<float>();
        var vector = new float[EmbedderDimensions.Dimension];
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = output[0, 0, i];
        }

        var norm = 0.0;
        foreach (var x in vector) norm += (double)x * x;
        norm = Math.Sqrt(norm);
        if (norm > 0)
        {
            for (var i = 0; i < vector.Length; i++) vector[i] = (float)(vector[i] / norm);
        }

        return vector;
    }

    public void Dispose()
    {
        _session.Dispose();
    }
}
