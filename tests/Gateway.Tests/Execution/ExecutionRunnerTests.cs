using Gateway.Audit;
using Gateway.Execution;
using Gateway.Governance;
using Gateway.Nlp.Orchestrator;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Execution;

public class ExecutionRunnerTests
{
    [Fact]
    public async Task Success_publishes_events_and_appends_one_audit_row()
    {
        var audit = new InMemoryAuditLogStore();
        var events = new RecordingSink();
        var transport = new StubTransport(request => new TabularResult(
            ["name"], [new Dictionary<string, string?> { ["name"] = "a" }], "Mongo", 7));
        var runner = new ExecutionRunner(transport, audit, events, Options(), TimeProvider.System);

        var result = await runner.ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(1, result.RowCount);
        Assert.Equal(["agent.executing", "agent.completed"], events.Events.Select(entry => entry.Name).ToArray());
        Assert.Single(await audit.ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Timeout_is_cancelled_and_audited_as_a_failure()
    {
        var audit = new InMemoryAuditLogStore();
        var events = new RecordingSink();
        var runner = new ExecutionRunner(new HangingTransport(), audit, events, Options(30), TimeProvider.System);

        var result = await runner.ExecuteAsync(Request(), CancellationToken.None);

        Assert.True(result.TimedOut);
        Assert.NotNull(result.Error);
        var audited = Assert.Single(await audit.ListAsync(CancellationToken.None));
        Assert.NotNull(audited.ExecutionDetails.Error);
        Assert.Equal("failed", events.Events[^1].Status);
    }

    [Fact]
    public async Task Transport_failure_is_reported_and_audited()
    {
        var audit = new InMemoryAuditLogStore();
        var events = new RecordingSink();
        var runner = new ExecutionRunner(new ThrowingTransport(), audit, events, Options(), TimeProvider.System);

        var result = await runner.ExecuteAsync(Request(), CancellationToken.None);

        Assert.False(result.TimedOut);
        Assert.Equal("mongo down", result.Error);
        Assert.Single(await audit.ListAsync(CancellationToken.None));
    }

    private static IOptions<ExecutionOptions> Options(int timeoutMs = 5000)
    {
        return Microsoft.Extensions.Options.Options.Create(new ExecutionOptions { TimeoutMs = timeoutMs });
    }

    private static ExecutionRequest Request()
    {
        return new ExecutionRequest(
            "[{\"$match\":{}}]",
            "sess-1",
            "Listings",
            ExecutionDataSource.Mongo,
            "listingsAndReviews",
            ["name"],
            new Dictionary<string, string>(),
            false,
            false,
            0,
            new RequesterContext("usr_1", "Bikash", "Business Analyst", null, "bnayak@enterprise.com"),
            GovernanceDecision.None);
    }

    private sealed class StubTransport : IQueryTransport
    {
        private readonly Func<ExecutionRequest, TabularResult> _result;

        public StubTransport(Func<ExecutionRequest, TabularResult> result)
        {
            _result = result;
        }

        public Task<TabularResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_result(request));
        }
    }

    private sealed class HangingTransport : IQueryTransport
    {
        public async Task<TabularResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return TabularResult.Empty("Mongo");
        }
    }

    private sealed class ThrowingTransport : IQueryTransport
    {
        public Task<TabularResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("mongo down");
        }
    }

    private sealed class RecordingSink : IAgentEventSink
    {
        public List<AgentEvent> Events { get; } = [];

        public void Publish(AgentEvent agentEvent)
        {
            Events.Add(agentEvent);
        }

        public IAsyncEnumerable<AgentEvent> ReadAllAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
