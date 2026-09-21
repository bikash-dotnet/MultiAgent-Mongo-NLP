using Gateway.Conversations;
using Gateway.Governance;
using Gateway.Nlp.Guardrails;
using Gateway.Nlp.Http;
using Gateway.Nlp.Orchestrator;
using Gateway.Nlp.Router;
using Gateway.Reports;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Conversations;

public class ConversationOrchestratorTests
{
    private sealed class StubOrchestrator : INlpOrchestrator
    {
        private readonly NlpRouteResult _result;

        public StubOrchestrator(NlpRouteResult result) => _result = result;

        public Task<NlpRouteResult> OrchestrateAsync(string utterance, string sessionId = "anonymous", CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
    }

    private static NlpRouteResult Report(string mql) => new(
        NlpRouteKind.ComplexLlmRequired,
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
        null,
        null,
        null);

    private static ConversationOrchestrator Build(INlpOrchestrator nlp, INotificationSender sender)
    {
        return new ConversationOrchestrator(
            nlp,
            new InMemoryConversationStore(),
            new InMemoryAccessRequestStore(),
            new InMemoryApprovalFlagStore(true),
            sender,
            new ColumnCatalog(),
            new InMemoryAgentEventSink(),
            Options.Create(new ReportsOptions()),
            TimeProvider.System);
    }

    [Fact]
    public async Task Report_query_starts_at_the_email_step()
    {
        var sut = Build(new StubOrchestrator(Report("""[{"$group":{"_id":"$address.market"}}]""")), new SimulatedNotificationSender());

        var turn = await sut.StartAsync("average price by market", "sess_1", "analyst@enterprise.com");

        Assert.Equal("Email", turn.Step);
        Assert.Equal("email", turn.Control);
        Assert.Equal("analyst@enterprise.com", turn.EmailPrefill);
        Assert.Equal("ComplexLlmRequired", turn.Kind);
    }

    [Fact]
    public async Task Invalid_email_does_not_advance()
    {
        var sut = Build(new StubOrchestrator(Report("""[{"$group":{"_id":"$address.market"}}]""")), new SimulatedNotificationSender());
        var turn = await sut.StartAsync("average price by market", "sess_1", null);

        var next = await sut.AnswerAsync(turn.ConversationId, new ConversationAnswer(Email: "not-an-email"));

        Assert.Equal("Email", next!.Step);
        Assert.NotNull(next.ValidationError);
    }

    [Fact]
    public async Task Valid_steps_reach_completion_and_build_a_csv_report()
    {
        var sender = new SimulatedNotificationSender();
        var sut = Build(new StubOrchestrator(Report("""[{"$group":{"_id":"$address.market"}}]""")), sender);

        var email = await sut.StartAsync("average price by market", "sess_1", null);
        var purpose = await sut.AnswerAsync(email.ConversationId, new ConversationAnswer(Email: "analyst@enterprise.com"));
        var manager = await sut.AnswerAsync(email.ConversationId, new ConversationAnswer(Purpose: "Quarterly review", ProjectCode: "PROJ-1"));
        var columns = await sut.AnswerAsync(email.ConversationId, new ConversationAnswer(ManagerEmail: "manager@enterprise.com"));
        var delivery = await sut.AnswerAsync(email.ConversationId, new ConversationAnswer(Columns: ["name", "price"]));
        var complete = await sut.AnswerAsync(email.ConversationId, new ConversationAnswer(Delivery: ReportIntake.Csv));

        Assert.Equal("Purpose", purpose!.Step);
        Assert.Equal("ManagerEmail", manager!.Step);
        Assert.Equal("Columns", columns!.Step);
        Assert.Equal("Delivery", delivery!.Step);
        Assert.Equal("Complete", complete!.Step);
        Assert.False(complete.ApprovalRequired);
        Assert.True(complete.Downloadable);
        Assert.Empty(sender.Sent);
    }
}
