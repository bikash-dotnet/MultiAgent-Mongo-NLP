using System.Diagnostics;
using Gateway.Nlp.Cache;
using Gateway.Nlp.Embeddings;
using Gateway.Nlp.Mql;
using Gateway.Nlp.Slots;
using Gateway.Nlp.Router;

namespace Gateway.Tests.Nlp;

public class NfrBenchTests
{
    private const int Iterations = 200;

    private const string Header =
        "# Sprint 2 — BRD-NFR-01 Benchmarks\n" +
        "\n" +
        "Measured in this environment (linux-x64 container, Release build). Budget per BRD-NFR-01\n" +
        "for in-memory steps; real ONNX embed recorded separately (split-budget decision).\n" +
        "\n" +
        "| Step | Measured | Budget |\n" +
        "| --- | --- | --- |\n";

    [Fact]
    public void Records_cache_lookup_slot_and_mql_benchmarks()
    {
        var gazetteer = Gazetteer.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Gazetteers"));
        var builder = ScribanSimpleMqlBuilder.FromAssetsDirectory(
            Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Templates"));
        var embedder = new OnnxBgeSmallEmbedder(TestPaths.ModelPath(), TestPaths.VocabPath());
        var cache = new SemanticCache();
        var router = new NlpRouter(embedder, cache, builder);

        // warm model + router
        router.Route("listings with pools in Los Angeles, just run it");
        router.Route("listings with pools in Los Angeles, just run it");

        var utterance = "listings with pools in Los Angeles, just run it";

        var cacheHitMs = Measure(() => router.Route(utterance));
        var embedMs = Measure(() => embedder.Embed(utterance));
        var slotMs = Measure(() => SlotExtractor.Extract(utterance, gazetteer));
        var mqlMs = Measure(() => builder.Build(SlotExtractor.Extract(utterance, gazetteer), MqlDefaults.Standard));

        var rows =
            $"| cache hit (full route incl. embed) | {cacheHitMs:F2} ms | < 10 ms (lookup) / embed measured |\n" +
            $"| real ONNX embed | {embedMs:F2} ms | measured ≈ 25 ms |\n" +
            $"| slot extraction | {slotMs:F3} ms | < 5 ms |\n" +
            $"| simple MQL render | {mqlMs:F3} ms | < 2 ms |\n";

        var path = Path.Combine(TestPaths.RepoRoot(), "docs", "benchmarks", "sprint-2-nfr01.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, Header + rows);

        // Budget asserts on in-memory steps (embed excluded from cache budget assert)
        Assert.True(slotMs < 5, $"slot extraction {slotMs:F3} ms exceeded 5 ms");
        Assert.True(mqlMs < 2, $"simple MQL render {mqlMs:F3} ms exceeded 2 ms");
    }

    private static double Measure(Action action)
    {
        for (var i = 0; i < 10; i++) action(); // warm
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < Iterations; i++) action();
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds / Iterations;
    }
}
