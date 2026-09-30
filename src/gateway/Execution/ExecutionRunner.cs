using Gateway.Audit;
using Gateway.Nlp.Orchestrator;
using Microsoft.Extensions.Options;

namespace Gateway.Execution;

public sealed class ExecutionRunner : ITabularQueryExecutor
{
    private readonly IQueryTransport _transport;
    private readonly IAuditLogStore _audit;
    private readonly IAgentEventSink _events;
    private readonly ExecutionOptions _options;
    private readonly TimeProvider _clock;

    public ExecutionRunner(
        IQueryTransport transport,
        IAuditLogStore audit,
        IAgentEventSink events,
        IOptions<ExecutionOptions> options,
        TimeProvider clock)
    {
        _transport = transport;
        _audit = audit;
        _events = events;
        _options = options.Value;
        _clock = clock;
    }

    public async Task<TabularResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
    {
        _events.Publish(new AgentEvent("agent.executing", "executing", request.DataSource.ToString()));
        var started = _clock.GetTimestamp();
        TabularResult result;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.TimeoutMs);

        try
        {
            result = await _transport.ExecuteAsync(request, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            result = new TabularResult(
                [],
                [],
                request.DataSource.ToString(),
                Elapsed(started),
                TimedOut: true,
                Error: $"Query exceeded the {_options.TimeoutMs} ms execution limit.");
        }
        catch (Exception error)
        {
            result = new TabularResult([], [], request.DataSource.ToString(), Elapsed(started), false, error.Message);
        }

        try
        {
            await _audit.AppendAsync(
                AuditLogFactory.Build(string.Empty, _clock.GetUtcNow(), request, result),
                cancellationToken);
        }
        catch (Exception error)
        {
            var message = $"Audit write failed: {error.Message}";
            result = result with { Error = result.Error is null ? message : $"{result.Error}; {message}" };
        }

        _events.Publish(new AgentEvent(
            "agent.completed",
            result.TimedOut || result.Error is not null ? "failed" : "completed",
            $"{result.RowCount} rows in {result.DurationMs} ms"));

        return result;
    }

    private long Elapsed(long started)
    {
        return (long)_clock.GetElapsedTime(started).TotalMilliseconds;
    }
}
