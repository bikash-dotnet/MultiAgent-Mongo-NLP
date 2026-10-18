using Gateway.Configuration;
using System.Diagnostics;
using Gateway.Nlp.Abstractions;
using Gateway.Nlp.Cache;
using Gateway.Nlp.Guardrails;
using Gateway.Nlp.Llm;
using Gateway.Nlp.Mql;
using Gateway.Nlp.Orchestrator;
using Gateway.Nlp.Router;
using Gateway.Nlp.Slots;
using Gateway.Persistence;
using Gateway.Rules;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Gateway.Tests.Nlp;

public class Sprint3BenchTests
{
    private const int Iterations = 200;

    private sealed class ConstEmbedder : ITextEmbedder
    {
        public float[] Embed(string text) => new float[] { 1, 0, 0, 0, 0, 0, 0, 0 };
    }

    private sealed class FixedGenerator : ILlmQueryGenerator
    {
        public Task<LlmQueryResult> GenerateAsync(string utterance, string slotsJson, string? previousError, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LlmQueryResult("""[{"$group":{"_id":"$address.market","averagePrice":{"$avg":"$price"}}}]""", 40));
        }
    }

    [Fact]
    public async Task Records_orchestration_overhead_for_complex_queries()
    {
        var gazetteer = Gazetteer.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Gazetteers"));
        var builder = ScribanSimpleMqlBuilder.FromAssetsDirectory(Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Templates"));
        var router = new NlpRouter(new ConstEmbedder(), new SemanticCache(new SettingsProvider()), builder, gazetteer);
        var provider = new SettingsProvider();
        provider.UpdateSection("NvidiaNim", JsonSerializer.SerializeToElement(new { MaxAttempts = 3 }), 1);
        var corrector = new SelfCorrectingLlmQueryGenerator(new FixedGenerator(), new PipelineValidator(), provider);
        var orchestrator = new NlpOrchestrator(router, gazetteer, corrector, new InMemoryAgentEventSink(),
            new InMemoryAgentStateStore(), GuardrailTestFactory.FromAssets(), new InMemoryAccessRequestStore(),
            TimeProvider.System, new BusinessRuleEngine(new BusinessRuleStore(new InMemoryDocumentStore())));

        // warm
        for (var i = 0; i < 10; i++)
        {
            await orchestrator.OrchestrateAsync("average price by market under $200");
        }

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < Iterations; i++)
        {
            await orchestrator.OrchestrateAsync("average price by market under $200");
        }
        sw.Stop();
        var perCall = sw.Elapsed.TotalMilliseconds / Iterations;

        var content =
            "# Sprint 3 — BRD-NFR-02 Complex MQL Benchmarks\n" +
            "\n" +
            "Orchestration overhead only (validator + self-correction loop with a fixed in-process generator).\n" +
            "Live NVIDIA NIM network latency (budget 1.5–3 s) is measured manually with a configured key.\n" +
            "\n" +
            "| Step | Measured | Budget |\n" +
            "| --- | --- | --- |\n" +
            $"| complex route (validation + single attempt, no network) | {perCall:F2} ms | n/a (excludes LLM) |\n";

        // Benchmarks are recorded to a temp path so test runs do not dirty the repository.
        var path = Path.Combine(Path.GetTempPath(), "sprint-3-nfr02.md");
        File.WriteAllText(path, content);

        Assert.True(perCall < 50, $"orchestration overhead {perCall:F2} ms exceeded 50 ms");
    }
}
