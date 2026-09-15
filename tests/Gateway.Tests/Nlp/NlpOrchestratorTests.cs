using Gateway.Nlp.Abstractions;
using Gateway.Nlp.Cache;
using Gateway.Nlp.Llm;
using Gateway.Nlp.Mql;
using Gateway.Nlp.Orchestrator;
using Gateway.Nlp.Router;
using Gateway.Nlp.Slots;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Nlp;

public class NlpOrchestratorTests
{
    private sealed class StubGenerator : ILlmQueryGenerator
    {
        private readonly string _pipeline;
        public int Calls { get; private set; }

        public StubGenerator(string pipeline) => _pipeline = pipeline;

        public Task<LlmQueryResult> GenerateAsync(string utterance, string slotsJson, string? previousError, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new LlmQueryResult(_pipeline, 7));
        }
    }

    private sealed class VectorEmbedder : ITextEmbedder
    {
        public float[] Embed(string text) => new float[] { 1, 0, 0, 0, 0, 0, 0, 0 };
    }

    private static (NlpOrchestrator Sut, StubGenerator Generator, InMemoryAgentEventSink Events) Build(string pipeline)
    {
        var gazetteer = Gazetteer.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Gazetteers"));
        var builder = ScribanSimpleMqlBuilder.FromAssetsDirectory(Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Templates"));
        var router = new NlpRouter(new VectorEmbedder(), new SemanticCache(), builder, gazetteer);
        var generator = new StubGenerator(pipeline);
        var events = new InMemoryAgentEventSink();
        var corrector = new SelfCorrectingLlmQueryGenerator(generator, new PipelineValidator(),
            Options.Create(new NvidiaNimOptions { MaxAttempts = 3 }));
        var sut = new NlpOrchestrator(router, gazetteer, corrector, events, new InMemoryAgentStateStore());
        return (sut, generator, events);
    }

    [Fact]
    public async Task Simple_query_skips_llm_and_emits_started_and_completed()
    {
        var (sut, generator, events) = Build("""[{"$limit":5}]""");

        var result = await sut.OrchestrateAsync("listings with pools in Los Angeles, just run it");

        Assert.Equal(NlpRouteKind.SimpleMql, result.Kind);
        Assert.Equal(0, generator.Calls);
        var names = await Read(events, 2);
        Assert.Equal(["agent.started", "agent.completed"], names);
    }

    [Fact]
    public async Task Complex_query_synthesizes_pipeline_with_llm()
    {
        var (sut, generator, _) = Build("""[{"$group":{"_id":"$address.market","averagePrice":{"$avg":"$price"}}}]""");

        var result = await sut.OrchestrateAsync("average price by market");

        Assert.Equal(NlpRouteKind.ComplexLlmRequired, result.Kind);
        Assert.Contains("$group", result.Mql);
        Assert.Equal(1, generator.Calls);
        Assert.Equal(1, result.LlmAttempts);
        Assert.True(result.LlmTokensConsumed > 0);
    }

    [Fact]
    public async Task Complex_query_failing_validation_returns_error_after_three_attempts()
    {
        var (sut, generator, _) = Build("not json");

        var result = await sut.OrchestrateAsync("average price by market");

        Assert.Equal(NlpRouteKind.ComplexLlmFailed, result.Kind);
        Assert.Null(result.Mql);
        Assert.Equal(3, result.LlmAttempts);
        Assert.Equal(3, generator.Calls);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public async Task Clarify_emits_single_clarifying_event()
    {
        var (sut, _, events) = Build("""[{"$limit":5}]""");

        var result = await sut.OrchestrateAsync("what can you do?");

        Assert.Equal(NlpRouteKind.ClarifyRequired, result.Kind);
        Assert.NotNull(result.Question);
        var names = await Read(events, 2);
        Assert.Equal(["agent.started", "agent.clarifying"], names);
    }

    private static async Task<List<string>> Read(IAgentEventSink sink, int count)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var names = new List<string>();
        await foreach (var agentEvent in sink.ReadAllAsync(cts.Token))
        {
            names.Add(agentEvent.Name);
            if (names.Count == count)
            {
                break;
            }
        }

        return names;
    }
}
