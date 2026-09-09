using Gateway.Nlp.Cache;

namespace Gateway.Tests.Nlp;

public class CosineSimilarityTests
{
    [Fact]
    public void Identical_vectors_score_one()
    {
        var v = new float[] { 1f, 0f, 0f };

        Assert.Equal(1.0, CosineSimilarity.Cosine(v, v), 6);
    }

    [Fact]
    public void Orthogonal_vectors_score_zero()
    {
        var a = new float[] { 1f, 0f, 0f };
        var b = new float[] { 0f, 1f, 0f };

        Assert.Equal(0.0, CosineSimilarity.Cosine(a, b), 6);
    }

    [Theory]
    [InlineData(0.951, true)]
    [InlineData(0.95, false)]
    [InlineData(0.5, false)]
    [InlineData(1.0, true)]
    public void Above_threshold_is_strictly_greater(double cosine, bool expected)
    {
        var a = new float[] { (float)cosine, (float)Math.Sqrt(1 - cosine * cosine), 0f };
        var b = new float[] { 1f, 0f, 0f };

        Assert.Equal(expected, CosineSimilarity.IsAbove(a, b, CacheDefaults.Threshold));
    }
}
