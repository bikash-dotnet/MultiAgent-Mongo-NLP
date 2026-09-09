using Gateway.Nlp.Cache;
using Gateway.Nlp.Embeddings;

namespace Gateway.Tests.Nlp;

public class OnnxBgeSmallEmbedderTests
{
    private readonly OnnxBgeSmallEmbedder _embedder;

    public OnnxBgeSmallEmbedderTests()
    {
        Assert.True(File.Exists(TestPaths.ModelPath()), "Run scripts/download-nlp-assets.sh first.");
        Assert.True(File.Exists(TestPaths.VocabPath()), "Run scripts/download-nlp-assets.sh first.");
        _embedder = new OnnxBgeSmallEmbedder(TestPaths.ModelPath(), TestPaths.VocabPath());
    }

    [Fact]
    public void Returns_384_dimension_l2_normalized_vector()
    {
        var v = _embedder.Embed("listings with pools in Los Angeles");

        Assert.Equal(EmbedderDimensions.Dimension, v.Length);
        var norm = Math.Sqrt(v.Sum(x => (double)x * x));
        Assert.Equal(1.0, norm, 4);
    }

    [Fact]
    public void Identical_text_scores_cosine_one()
    {
        var a = _embedder.Embed("listings with pools in Los Angeles");
        var b = _embedder.Embed("listings with pools in Los Angeles");

        Assert.True(CosineSimilarity.IsAbove(a, b, 0.999));
    }

    [Fact]
    public void Near_paraphrase_scores_above_095()
    {
        var a = _embedder.Embed("listings with pools in Los Angeles");
        var b = _embedder.Embed("listings that have pools in Los Angeles");

        Assert.True(CosineSimilarity.IsAbove(a, b, 0.95));
    }

    [Fact]
    public void Distinct_query_scores_well_below_095()
    {
        var a = _embedder.Embed("listings with pools in Los Angeles");
        var b = _embedder.Embed("luxury condos in New York under 500");

        Assert.False(CosineSimilarity.IsAbove(a, b, 0.95));
        Assert.True(CosineSimilarity.Cosine(a, b) < 0.8);
    }
}
