using Gateway.Conversations;
using Gateway.Governance;
using Gateway.Nlp.Guardrails;
using Gateway.Nlp.Http;
using Gateway.Nlp.Orchestrator;
using Gateway.Nlp.Router;
using Gateway.Reports;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Conversations;

public class ConversationResumeTests
{
    private sealed class StubOrchestrator : INlpOrchestrator
    {
        public Task<NlpRouteResult> OrchestrateAsync(
            string utterance,
            string sessionId = "anonymous",
            CancellationToken cancellationToken = default,
            RequesterContext? requester = null) =>
            throw new NotImplementedException();
    }

    private static (ConversationOrchestrator Sut, IConversationStore Store) Build()
    {
        var store = new InMemoryConversationStore();
        var sut = new ConversationOrchestrator(
            new StubOrchestrator(),
            store,
            new InMemoryAccessRequestStore(),
            new InMemoryApprovalFlagStore(true),
            new SimulatedNotificationSender(),
            new ColumnCatalog(),
            new InMemoryAgentEventSink(),
            Options.Create(new ReportsOptions()),
            TimeProvider.System);
        return (sut, store);
    }

    private static ConversationState Paused(string id, string requestId, string delivery) => new(
        id,
        "sess_1",
        ConversationStep.Complete,
        "average coordinates near me",
        "[]",
        NlpRouteKind.GovernancePaused,
        requestId,
        ["address.location.coordinates"],
        false,
        true,
        new ReportIntakeDraft(
            RequesterEmail: "analyst@enterprise.com",
            Purpose: "Geo analysis",
            ProjectCode: "PROJ-1",
            ManagerEmail: "manager@enterprise.com",
            Columns: ["name"],
            DeliveryFormat: delivery),
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task Resume_lifts_the_hold_and_unlocks_csv()
    {
        var (sut, store) = Build();
        await store.CreateAsync(Paused("conv_1", "req_1", ReportIntake.Csv));

        var resumed = await sut.ResumeAsync("req_1");

        Assert.True(resumed);
        var turn = await sut.GetAsync("conv_1");
        Assert.False(turn!.ApprovalRequired);
        Assert.True(turn.Downloadable);
    }

    [Fact]
    public async Task Resume_without_a_conversation_is_a_no_op()
    {
        var (sut, _) = Build();

        var resumed = await sut.ResumeAsync("missing");

        Assert.False(resumed);
    }
}
