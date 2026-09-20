using Gateway.Nlp.Abstractions;
using Gateway.Nlp.Cache;
using Gateway.Nlp.Guardrails;
using Gateway.Nlp.Llm;
using Gateway.Nlp.Mql;
using Gateway.Nlp.Orchestrator;
using Gateway.Nlp.Router;
using Gateway.Nlp.Slots;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Nlp;

public class NlpOrchestratorGuardrailTests
{
    private sealed class StubGenerator : ILlmQueryGenerator
    {
        private readonly string _pipeline;

        public StubGenerator(string pipeline) => _pipeline = pipeline;

        public Task<LlmQueryResult> GenerateAsync(string utterance, string slotsJson, string? previousError, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LlmQueryResult(_pipeline, 7));
        }
    }

    private sealed class VectorEmbedder : ITextEmbedder
    {
        public float[] Embed(string text) => new float[] { 1, 0, 0, 0, 0, 0, 0, 0 };
    }

    private static (NlpOrchestrator Sut, InMemoryAgentEventSink Events, InMemoryAccessRequestStore Requests) Build(string pipeline)
    {
        var gazetteer = Gazetteer.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Gazetteers"));
        var builder = ScribanSimpleMqlBuilder.FromAssetsDirectory(Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Templates"));
        var router = new NlpRouter(new VectorEmbedder(), new SemanticCache(), builder, gazetteer);
        var corrector = new SelfCorrectingLlmQueryGenerator(new StubGenerator(pipeline), new PipelineValidator(),
            Options.Create(new NvidiaNimOptions { MaxAttempts = 3 }));
        var events = new InMemoryAgentEventSink();
        var requests = new InMemoryAccessRequestStore();
        var sut = new NlpOrchestrator(router, gazetteer, corrector, events, new InMemoryAgentStateStore(),
            GuardrailTestFactory.FromAssets(), requests, TimeProvider.System);
        return (sut, events, requests);
    }

    [Fact]
    public async Task Sensitive_query_pauses_and_creates_pending_request()
    {
        var (sut, events, requests) = Build("""[{"$match":{"address.location.coordinates.lat":{"$gte":40}}}]""");

        var result = await sut.OrchestrateAsync("average coordinates near me");

        Assert.Equal(NlpRouteKind.GovernancePaused, result.Kind);
        Assert.Contains("address.location.coordinates", result.SensitiveFields!);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessRequestId));

        var stored = await requests.GetAsync(result.AccessRequestId!);
        Assert.NotNull(stored);
        Assert.Equal(AccessRequest.PendingLead, stored!.Status);

        var names = await Read(events, 2);
        Assert.Equal(["agent.started", "governance.paused"], names);
    }

    [Fact]
    public async Task Unknown_field_query_is_rejected()
    {
        var (sut, _, _) = Build("""[{"$match":{"secrets.token":"x"}}]""");

        var result = await sut.OrchestrateAsync("average secrets near me");

        Assert.Equal(NlpRouteKind.Rejected, result.Kind);
        Assert.False(string.IsNullOrWhiteSpace(result.GuardrailReason));
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
