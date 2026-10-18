using Gateway.Audit;
using Gateway.Configuration;
using Gateway.Nlp.Orchestrator;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Gateway.Execution;

public sealed class ExecutionRunner : ITabularQueryExecutor
{
    private readonly IQueryTransport _transport;
    private readonly IAuditLogStore _audit;
    private readonly IAgentEventSink _events;
    private readonly IOptions<ExecutionOptions> _options;
    private readonly ISettingsProvider _provider;
    private readonly TimeProvider _clock;

    public ExecutionRunner(
        IQueryTransport transport,
        IAuditLogStore audit,
        IAgentEventSink events,
        IOptions<ExecutionOptions> options,
        ISettingsProvider provider,
        TimeProvider clock)
    {
        _transport = transport;
        _audit = audit;
        _events = events;
        _options = options;
        _provider = provider;
        _clock = clock;
    }

    public async Task<TabularResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
    {
        _events.Publish(new AgentEvent("agent.executing", "executing", request.DataSource.ToString()));
        var started = _clock.GetTimestamp();
        TabularResult result;

        var timeoutMs = GetTimeoutMs();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(timeoutMs);

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
                Error: $"Query exceeded the {timeoutMs} ms execution limit.");
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

    private int GetTimeoutMs()
    {
        if (_provider.TryGetValue("Execution", "TimeoutMs", out var value) &&
            value.ValueKind == JsonValueKind.Number)
        {
            return value.GetInt32();
        }
        return _options.Value.TimeoutMs;
    }

    private long Elapsed(long started)
    {
        return (long)_clock.GetElapsedTime(started).TotalMilliseconds;
    }
}
