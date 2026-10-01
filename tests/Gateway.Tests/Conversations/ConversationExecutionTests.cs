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

public class ConversationExecutionTests
{
    private sealed class StubOrchestrator : INlpOrchestrator
    {
        public Task<NlpRouteResult> OrchestrateAsync(string utterance, string sessionId = "anonymous", CancellationToken cancellationToken = default, RequesterContext? requester = null)
            => Task.FromResult(new NlpRouteResult(
                NlpRouteKind.ComplexLlmRequired,
                """[{"$group":{"_id":"$address.market"}}]""",
                null,
                false,
                false,
                Gateway.Nlp.Intent.IntentKind.Search,
                false,
                new MqlDefaults(10, "rating_desc", "All"),
                0,
                1,
                null,
                null,
                null,
                null));
    }

    private sealed class StubExecutor : ITabularQueryExecutor
    {
        public Task<TabularResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new TabularResult(
                ["name", "price"],
                [new Dictionary<string, string?> { ["name"] = "real-row", ["price"] = "120" }],
                request.DataSource.ToString(),
                3));
    }

    private static ConversationOrchestrator Build()
    {
        return new ConversationOrchestrator(
            new StubOrchestrator(),
            new InMemoryConversationStore(),
            new InMemoryAccessRequestStore(),
            new InMemoryApprovalFlagStore(true),
            new SimulatedNotificationSender(),
            new ColumnCatalog(),
            new InMemoryAgentEventSink(),
            Options.Create(new ReportsOptions()),
            TimeProvider.System,
            new StubExecutor(),
            Options.Create(new ExecutionOptions()));
    }

    [Fact]
    public async Task Completing_a_report_executes_and_carries_rows()
    {
        var sut = Build();

        var email = await sut.StartAsync("average price by market", "sess_1", null);
        await sut.AnswerAsync(email.ConversationId, new ConversationAnswer(Email: "analyst@enterprise.com"));
        await sut.AnswerAsync(email.ConversationId, new ConversationAnswer(Purpose: "Quarterly review", ProjectCode: "PROJ-1"));
        await sut.AnswerAsync(email.ConversationId, new ConversationAnswer(ManagerEmail: "manager@enterprise.com"));
        await sut.AnswerAsync(email.ConversationId, new ConversationAnswer(Columns: ["name", "price"]));
        var complete = await sut.AnswerAsync(email.ConversationId, new ConversationAnswer(Delivery: ReportIntake.Csv));

        Assert.NotNull(complete!.Execution);
        Assert.Equal(1, complete.Execution!.RowCount);
        Assert.Equal("real-row", complete.Execution.Rows[0]["name"]);
    }
}
