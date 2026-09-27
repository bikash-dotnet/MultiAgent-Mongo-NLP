using Gateway.Conversations;
using Gateway.Governance;
using Gateway.Nlp.Guardrails;
using Gateway.Nlp.Intent;
using Gateway.Nlp.Mql;
using Gateway.Nlp.Orchestrator;
using Gateway.Nlp.Router;
using Gateway.Nlp.Http;
using Gateway.Reports;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Conversations;

public class ConversationRequesterTests
{
    private sealed class StubOrchestrator : INlpOrchestrator
    {
        public RequesterContext? LastRequester { get; private set; }

        public Task<NlpRouteResult> OrchestrateAsync(
            string utterance,
            string sessionId = "anonymous",
            CancellationToken cancellationToken = default,
            RequesterContext? requester = null)
        {
            LastRequester = requester;
            var result = new NlpRouteResult(
                NlpRouteKind.GovernancePaused,
                "[]",
                null,
                false,
                false,
                IntentKind.Search,
                false,
                new MqlDefaults(10, "rating_desc", "All"),
                0,
                1,
                null,
                ["address.location.coordinates"],
                "req_1",
                "sensitive field requires approval: address.location.coordinates");
            return Task.FromResult(result);
        }
    }

    [Fact]
    public async Task Start_passes_the_requester_context_to_the_orchestrator()
    {
        var stub = new StubOrchestrator();
        var sut = new ConversationOrchestrator(
            stub,
            new InMemoryConversationStore(),
            new InMemoryAccessRequestStore(),
            new InMemoryApprovalFlagStore(true),
            new SimulatedNotificationSender(),
            new ColumnCatalog(),
            new InMemoryAgentEventSink(),
            Options.Create(new ReportsOptions()),
            TimeProvider.System);

        await sut.StartAsync(
            "average coordinates near me",
            "sess_1",
            "analyst@enterprise.com",
            default,
            new RequesterContext("usr_analyst", "Analyst", "Business Analyst", "usr_lead"));

        Assert.NotNull(stub.LastRequester);
        Assert.Equal("usr_analyst", stub.LastRequester!.UserId);
    }
}
