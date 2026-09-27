using Gateway.Conversations;
using Gateway.Governance;
using Gateway.Nlp.Guardrails;
using Gateway.Nlp.Intent;
using Gateway.Nlp.Mql;
using Gateway.Nlp.Orchestrator;
using Gateway.Nlp.Router;
using Gateway.Reports;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Conversations;

public class ConversationBusinessImpactTests
{
    private sealed class StubOrchestrator : INlpOrchestrator
    {
        private readonly NlpRouteResult _result;

        public StubOrchestrator(NlpRouteResult result) => _result = result;

        public Task<NlpRouteResult> OrchestrateAsync(
            string utterance,
            string sessionId = "anonymous",
            CancellationToken cancellationToken = default,
            Gateway.Governance.RequesterContext? requester = null)
            => Task.FromResult(_result);
    }

    private static NlpRouteResult Paused() => new(
        NlpRouteKind.GovernancePaused,
        """[{"$match":{"address.location.coordinates.lat":{"$gte":40}}}]""",
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

    private static async Task<(ConversationOrchestrator Sut, ConversationTurn Turn)> StartAsync()
    {
        var sut = new ConversationOrchestrator(
            new StubOrchestrator(Paused()),
            new InMemoryConversationStore(),
            new InMemoryAccessRequestStore(),
            new InMemoryApprovalFlagStore(true),
            new SimulatedNotificationSender(),
            new ColumnCatalog(),
            new InMemoryAgentEventSink(),
            Options.Create(new ReportsOptions()),
            TimeProvider.System);
        var turn = await sut.StartAsync("average coordinates near me", "sess_1", "analyst@enterprise.com");
        turn = await sut.AnswerAsync(turn.ConversationId, new ConversationAnswer(Email: "analyst@enterprise.com"));
        return (sut, turn);
    }

    [Fact]
    public async Task Gated_intake_requires_a_business_impact_step()
    {
        var (sut, turn) = await StartAsync();

        turn = await sut.AnswerAsync(turn.ConversationId, new ConversationAnswer(Purpose: "Geo analysis", ProjectCode: "PROJ-1"));

        Assert.Equal("BusinessImpact", turn!.Step);
        Assert.Equal("businessimpact", turn.Control);
    }

    [Fact]
    public async Task Gated_purpose_requires_a_project_code()
    {
        var (sut, turn) = await StartAsync();

        turn = await sut.AnswerAsync(turn.ConversationId, new ConversationAnswer(Purpose: "Geo analysis"));

        Assert.Equal("Purpose", turn!.Step);
        Assert.Equal("A project code is required for sensitive requests.", turn.ValidationError);
    }

    [Fact]
    public async Task Business_impact_is_validated_and_stored()
    {
        var (sut, turn) = await StartAsync();
        turn = await sut.AnswerAsync(turn.ConversationId, new ConversationAnswer(Purpose: "Geo analysis", ProjectCode: "PROJ-1"));

        var invalid = await sut.AnswerAsync(turn.ConversationId, new ConversationAnswer(BusinessImpact: "short"));
        Assert.Equal("BusinessImpact", invalid!.Step);
        Assert.NotNull(invalid.ValidationError);

        var ok = await sut.AnswerAsync(turn.ConversationId, new ConversationAnswer(BusinessImpact: "Target market expansion planning"));
        Assert.Equal("ManagerEmail", ok!.Step);
    }
}
