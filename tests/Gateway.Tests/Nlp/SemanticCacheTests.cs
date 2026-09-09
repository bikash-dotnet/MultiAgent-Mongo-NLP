using Gateway.Nlp.Cache;

namespace Gateway.Tests.Nlp;

public class SemanticCacheTests
{
    private static float[] Unit(params double[] xs)
    {
        var v = xs.Select(x => (float)x).ToArray();
        var norm = Math.Sqrt(v.Sum(x => (double)x * x));
        return v.Select(x => (float)(x / norm)).ToArray();
    }

    [Fact]
    public void Miss_on_empty_cache()
    {
        var cache = new SemanticCache();
        Assert.Null(cache.TryFind(Unit(1, 0, 0)));
    }

    [Fact]
    public void Exact_repeat_is_a_hit()
    {
        var cache = new SemanticCache();
        cache.Store(Unit(1, 0, 0), "q", "[{\"$limit\":10}]");

        var hit = cache.TryFind(Unit(1, 0, 0));

        Assert.NotNull(hit);
        Assert.Equal("[{\"$limit\":10}]", hit!.Mql);
        Assert.Equal("q", hit.CanonicalQuery);
    }

    [Fact]
    public void Below_threshold_is_a_miss()
    {
        var cache = new SemanticCache();
        cache.Store(Unit(1, 0, 0), "q", "[{\"$limit\":10}]");

        Assert.Null(cache.TryFind(Unit(0.8, 0.6, 0)));
    }

    [Fact]
    public void Above_threshold_is_a_hit()
    {
        var cache = new SemanticCache();
        cache.Store(Unit(1, 0, 0), "q", "[{\"$limit\":10}]");

        var hit = cache.TryFind(Unit(0.96, 0.28, 0));

        Assert.NotNull(hit);
    }

    [Fact]
    public async Task Concurrent_stores_and_finds_do_not_throw()
    {
        var cache = new SemanticCache();
        var tasks = Enumerable.Range(0, 32)
            .Select(i => Task.Run(() =>
            {
                cache.Store(Unit(i + 1, 1, 0), $"q{i}", "[{\"$limit\":10}]");
                _ = cache.TryFind(Unit(i + 1, 1, 0));
            }));

        await Task.WhenAll(tasks);
        Assert.NotNull(cache.TryFind(Unit(5, 1, 0)));
    }
}
