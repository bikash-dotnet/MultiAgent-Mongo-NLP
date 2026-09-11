using Gateway.Nlp.Abstractions;
using Gateway.Nlp.Cache;
using Gateway.Nlp.Intent;
using Gateway.Nlp.Mql;
using Gateway.Nlp.Router;

namespace Gateway.Tests.Nlp;

public class NlpRouterTests
{
    private sealed class VectorEmbedder : ITextEmbedder
    {
        public Dictionary<string, float[]> Cache { get; } = new();

        public float[] Embed(string text)
        {
            if (Cache.TryGetValue(text, out var v)) return v;

            var h = text.GetHashCode();
            var vec = new float[8];
            for (var i = 0; i < vec.Length; i++)
            {
                vec[i] = (float)Math.Sin(h * (i + 1) * 0.13);
            }

            var norm = Math.Sqrt(vec.Sum(x => (double)x * x));
            for (var i = 0; i < vec.Length; i++) vec[i] = (float)(vec[i] / norm);

            Cache[text] = vec;
            return vec;
        }
    }

    private readonly VectorEmbedder _embedder = new();
    private readonly NlpRouter _router;
    private readonly IMqlBuilder _builder;
    private readonly ISemanticCache _cache;

    public NlpRouterTests()
    {
        _cache = new SemanticCache();
        _builder = ScribanSimpleMqlBuilder.FromAssetsDirectory(
            Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Templates"));
        _router = new NlpRouter(_embedder, _cache, _builder);
    }

    [Fact]
    public void Acceptance_phrase_returns_simple_mql_with_zero_tokens()
    {
        var result = _router.Route("listings with pools in Los Angeles, just run it");

        Assert.Equal(NlpRouteKind.SimpleMql, result.Kind);
        Assert.False(result.SemanticCacheHit);
        Assert.True(result.SlotExtractionUsed);
        Assert.Equal(IntentKind.Search, result.Intent);
        Assert.Equal(0, result.LlmTokensConsumed);
        Assert.Contains("address.market", result.Mql);
    }

    [Fact]
    public void Repeat_query_hits_semantic_cache()
    {
        _router.Route("listings with pools in Los Angeles, just run it");

        var result = _router.Route("listings with pools in Los Angeles, just run it");

        Assert.Equal(NlpRouteKind.CacheHit, result.Kind);
        Assert.True(result.SemanticCacheHit);
        Assert.Equal(0, result.LlmTokensConsumed);
    }

    [Fact]
    public void Complex_unstructured_query_is_complex()
    {
        var result = _router.Route("coziest neighborhoods near the beach by season");

        Assert.Equal(NlpRouteKind.ComplexLlmRequired, result.Kind);
        Assert.Null(result.Mql);
        Assert.Equal(0, result.LlmTokensConsumed);
    }

    [Fact]
    public void Clarify_intent_returns_single_question()
    {
        var result = _router.Route("what can you do?");

        Assert.Equal(NlpRouteKind.ClarifyRequired, result.Kind);
        Assert.NotNull(result.Question);
        Assert.Null(result.Mql);
    }

    [Fact]
    public void Just_run_it_with_sparse_slots_still_simple()
    {
        var result = _router.Route("listings, just run it");

        Assert.Equal(NlpRouteKind.SimpleMql, result.Kind);
        Assert.True(result.JustRunIt);
    }
}
