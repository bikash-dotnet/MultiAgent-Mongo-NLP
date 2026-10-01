using Gateway.Execution;
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

public class NlpOrchestratorExecutionTests
{
    private sealed class StubGenerator : ILlmQueryGenerator
    {
        public Task<LlmQueryResult> GenerateAsync(string utterance, string slotsJson, string? previousError, CancellationToken cancellationToken = default)
            => Task.FromResult(new LlmQueryResult("""[{"$limit":5}]""", 1));
    }

    private sealed class VectorEmbedder : ITextEmbedder
    {
        public float[] Embed(string text) => new float[] { 1, 0, 0, 0, 0, 0, 0, 0 };
    }

    private sealed class StubExecutor : ITabularQueryExecutor
    {
        public ExecutionRequest? Last { get; private set; }

        public Task<TabularResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
        {
            Last = request;
            return Task.FromResult(new TabularResult(
                ["name"],
                [new Dictionary<string, string?> { ["name"] = "listing-1" }],
                request.DataSource.ToString(),
                4));
        }
    }

    private static (NlpOrchestrator Sut, StubExecutor Executor) Build()
    {
        var gazetteer = Gazetteer.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Gazetteers"));
        var builder = ScribanSimpleMqlBuilder.FromAssetsDirectory(Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Templates"));
        var router = new NlpRouter(new VectorEmbedder(), new SemanticCache(), builder, gazetteer);
        var corrector = new SelfCorrectingLlmQueryGenerator(new StubGenerator(), new PipelineValidator(), Options.Create(new NvidiaNimOptions()));
        var executor = new StubExecutor();
        var sut = new NlpOrchestrator(
            router,
            gazetteer,
            corrector,
            new InMemoryAgentEventSink(),
            new InMemoryAgentStateStore(),
            GuardrailTestFactory.FromAssets(),
            new InMemoryAccessRequestStore(),
            TimeProvider.System,
            executor,
            Options.Create(new ExecutionOptions()));
        return (sut, executor);
    }

    [Fact]
    public async Task Simple_query_executes_and_returns_rows()
    {
        var (sut, executor) = Build();

        var result = await sut.OrchestrateAsync("listings with pools in Los Angeles, just run it", "sess_1");

        Assert.Equal(NlpRouteKind.SimpleMql, result.Kind);
        Assert.Equal(["name"], result.Columns);
        Assert.Equal(1, result.RowCount);
        Assert.Equal("Mongo", result.DataSource);
        Assert.Equal("sess_1", executor.Last!.SessionId);
    }
}
