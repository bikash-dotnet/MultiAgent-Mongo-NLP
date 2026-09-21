using Gateway.Conversations;
using Gateway.Governance;
using Gateway.Nlp.Guardrails;
using Gateway.Nlp.Orchestrator;
using Gateway.Nlp.Router;
using Gateway.Nlp.Http;
using Gateway.Reports;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Conversations;

public class ConversationOrchestratorApprovalTests
{
    private sealed class StubOrchestrator : INlpOrchestrator
    {
        private readonly NlpRouteResult _result;

        public StubOrchestrator(NlpRouteResult result) => _result = result;

        public Task<NlpRouteResult> OrchestrateAsync(string utterance, string sessionId = "anonymous", CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
    }

    private static NlpRouteResult Paused(string mql) => new(
        NlpRouteKind.GovernancePaused,
        mql,
        null,
        false,
        false,
        Gateway.Nlp.Intent.IntentKind.Search,
        false,
        new Gateway.Nlp.Mql.MqlDefaults(10, "rating_desc", "All"),
        0,
        1,
        null,
        ["address.location.coordinates"],
        "req_1",
        "sensitive field requires approval: address.location.coordinates");

    private static async Task<(ConversationTurn Turn, SimulatedNotificationSender Sender, IAccessRequestStore Requests)> RunAsync(NlpRouteResult result, string delivery = ReportIntake.Csv)
    {
        var sender = new SimulatedNotificationSender();
        var requests = new InMemoryAccessRequestStore();
        await requests.CreateAsync(new AccessRequest(
            "req_1",
            "sess_1",
            result.Mql!,
            ["address.location.coordinates"],
            AccessRequest.PendingLead,
            null,
            DateTimeOffset.UnixEpoch));

        var sut = new ConversationOrchestrator(
            new StubOrchestrator(result),
            new InMemoryConversationStore(),
            requests,
            new InMemoryApprovalFlagStore(true),
            sender,
            new ColumnCatalog(),
            new InMemoryAgentEventSink(),
            Options.Create(new ReportsOptions()),
            TimeProvider.System);

        var turn = await sut.StartAsync("average coordinates near me", "sess_1", "analyst@enterprise.com");
        turn = await sut.AnswerAsync(turn.ConversationId, new ConversationAnswer(Email: "analyst@enterprise.com"));
        turn = await sut.AnswerAsync(turn.ConversationId, new ConversationAnswer(Purpose: "Geo analysis"));
        turn = await sut.AnswerAsync(turn.ConversationId, new ConversationAnswer(ManagerEmail: "manager@enterprise.com"));
        turn = await sut.AnswerAsync(turn.ConversationId, new ConversationAnswer(Columns: ["name"]));
        turn = await sut.AnswerAsync(turn.ConversationId, new ConversationAnswer(Delivery: delivery));
        return (turn!, sender, requests);
    }

    [Fact]
    public async Task Sensitive_completion_requires_approval_and_notifies_the_manager()
    {
        var (turn, sender, requests) = await RunAsync(Paused("""[{"$match":{"address.location.coordinates.lat":{"$gte":40}}}]"""));

        Assert.True(turn.ApprovalRequired);
        Assert.False(turn.Downloadable);
        Assert.Contains("PENDING_LEAD", turn.AssistantMessage);
        Assert.Single(sender.Sent);
        Assert.Equal("manager_notification", sender.Sent[0].Kind);
        Assert.Equal("manager@enterprise.com", sender.Sent[0].Recipient);

        var stored = await requests.GetAsync("req_1");
        Assert.Equal("manager@enterprise.com", stored!.Intake!.ManagerEmail);
    }

    [Fact]
    public async Task Email_delivery_is_simulated_for_non_sensitive_reports()
    {
        var result = Paused("""[{"$match":{"address.location.coordinates.lat":{"$gte":40}}}]""") with
        {
            Kind = NlpRouteKind.ComplexLlmRequired,
            SensitiveFields = []
        };

        var (turn, sender, _) = await RunAsync(result, ReportIntake.Email);

        Assert.False(turn.ApprovalRequired);
        Assert.Single(sender.Sent);
        Assert.Equal("report_email", sender.Sent[0].Kind);
    }
}
