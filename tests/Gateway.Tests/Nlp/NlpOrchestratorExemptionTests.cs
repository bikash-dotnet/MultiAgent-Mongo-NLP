using Gateway.Governance;
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

public class NlpOrchestratorExemptionTests
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

    private static (NlpOrchestrator Sut, InMemoryAccessRequestStore Requests) Build()
    {
        var gazetteer = Gazetteer.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Gazetteers"));
        var builder = ScribanSimpleMqlBuilder.FromAssetsDirectory(Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Templates"));
        var router = new NlpRouter(new VectorEmbedder(), new SemanticCache(), builder, gazetteer);
        var corrector = new SelfCorrectingLlmQueryGenerator(
            new StubGenerator("""[{"$match":{"address.location.coordinates.lat":{"$gte":40}}}]"""),
            new PipelineValidator(),
            Options.Create(new NvidiaNimOptions { MaxAttempts = 3 }));
        var requests = new InMemoryAccessRequestStore();
        var sut = new NlpOrchestrator(
            router,
            gazetteer,
            corrector,
            new InMemoryAgentEventSink(),
            new InMemoryAgentStateStore(),
            GuardrailTestFactory.FromAssets(),
            requests,
            TimeProvider.System);
        return (sut, requests);
    }

    [Fact]
    public async Task Data_owner_does_not_create_a_pending_request()
    {
        var (sut, requests) = Build();

        var result = await sut.OrchestrateAsync(
            "average coordinates near me",
            "sess_1",
            default,
            new RequesterContext("usr_owner", "Owner", "Data Owner / Admin", null));

        Assert.NotEqual(NlpRouteKind.GovernancePaused, result.Kind);
        Assert.Empty(await requests.ListAsync());
    }

    [Fact]
    public async Task Analyst_pending_request_carries_requester_and_lead()
    {
        var (sut, requests) = Build();

        var result = await sut.OrchestrateAsync(
            "average coordinates near me",
            "sess_1",
            default,
            new RequesterContext("usr_analyst", "Analyst", "Business Analyst", "usr_lead"));

        Assert.Equal(NlpRouteKind.GovernancePaused, result.Kind);
        var stored = await requests.GetAsync(result.AccessRequestId!);
        Assert.NotNull(stored);
        Assert.Equal("usr_analyst", stored!.Requester!.UserId);
        Assert.Equal("usr_lead", stored.AssignedLeadId);
        Assert.Contains(stored.RequestedFlags!, flag => flag.FieldPath == "address.location.coordinates");
    }
}
