using Gateway.Conversations;
using Gateway.Execution;
using Gateway.Governance;
using Gateway.Nlp.Guardrails;
using Gateway.Nlp.Http;
using Gateway.Nlp.Mql;
using Gateway.Nlp.Orchestrator;
using Gateway.Nlp.Router;
using Gateway.Reports;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Conversations;

public class ConversationResumeExecutionTests
{
    private sealed class StubOrchestrator : INlpOrchestrator
    {
        public Task<NlpRouteResult> OrchestrateAsync(string utterance, string sessionId = "anonymous", CancellationToken cancellationToken = default, RequesterContext? requester = null)
            => Task.FromResult(new NlpRouteResult(
                NlpRouteKind.GovernancePaused,
                """[{"$match":{"address.location.coordinates":{"$exists":true}}}]""",
                null,
                false,
                false,
                Gateway.Nlp.Intent.IntentKind.Search,
                false,
                new MqlDefaults(10, "rating_desc", "All"),
                0,
                1,
                null,
                ["address.location.coordinates"],
                "req-1"));
    }

    private sealed class StubExecutor : ITabularQueryExecutor
    {
        public Task<TabularResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new TabularResult(
                ["name"],
                [new Dictionary<string, string?> { ["name"] = "after-approval" }],
                request.DataSource.ToString(),
                2));
    }

    [Fact]
    public async Task Resume_executes_and_stores_rows()
    {
        var store = new InMemoryConversationStore();
        var requests = new InMemoryAccessRequestStore();
        var created = await requests.CreateAsync(new AccessRequest(
            "req-1", "sess_1", """[{"$match":{}}]""", ["address.location.coordinates"], AccessRequest.Approved, null, DateTimeOffset.UnixEpoch));
        var sut = new ConversationOrchestrator(
            new StubOrchestrator(),
            store,
            requests,
            new InMemoryApprovalFlagStore(true),
            new SimulatedNotificationSender(),
            new ColumnCatalog(),
            new InMemoryAgentEventSink(),
            Options.Create(new ReportsOptions()),
            TimeProvider.System,
            new StubExecutor(),
            Options.Create(new ExecutionOptions()));

        var start = await sut.StartAsync("average coordinates near me", "sess_1", "analyst@enterprise.com");
        await sut.AnswerAsync(start.ConversationId, new ConversationAnswer(Email: "analyst@enterprise.com"));
        await sut.AnswerAsync(start.ConversationId, new ConversationAnswer(Purpose: "Quarterly review", ProjectCode: "PROJ-1"));
        await sut.AnswerAsync(start.ConversationId, new ConversationAnswer(BusinessImpact: "Revenue planning impact"));
        await sut.AnswerAsync(start.ConversationId, new ConversationAnswer(ManagerEmail: "manager@enterprise.com"));
        await sut.AnswerAsync(start.ConversationId, new ConversationAnswer(Columns: ["name"]));
        await sut.AnswerAsync(start.ConversationId, new ConversationAnswer(Delivery: ReportIntake.Csv));

        var resumed = await sut.ResumeAsync(created.Id);

        Assert.True(resumed);
        var turn = await sut.GetAsync(start.ConversationId);
        Assert.NotNull(turn!.Execution);
        Assert.Equal("after-approval", turn.Execution!.Rows[0]["name"]);
    }
}
