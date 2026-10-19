using Gateway.Audit;
using Gateway.Configuration;
using Gateway.Nlp.Orchestrator;
using Gateway.Rules;
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
    private readonly BusinessRuleEngine _rules;

    public ExecutionRunner(
        IQueryTransport transport,
        IAuditLogStore audit,
        IAgentEventSink events,
        IOptions<ExecutionOptions> options,
        ISettingsProvider provider,
        TimeProvider clock,
        BusinessRuleEngine rules)
    {
        _transport = transport;
        _audit = audit;
        _events = events;
        _options = options;
        _provider = provider;
        _clock = clock;
        _rules = rules;
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

            var ruleResult = await _rules.EvaluateAsync(request.User, schemaId: null, cancellationToken);
            var effects = ruleResult.Effects;

            var excluded = BusinessRuleEngine.ExcludedColumns(effects)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var masked = BusinessRuleEngine.MaskedColumns(effects)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var exemptRoles = BusinessRuleEngine.ExemptRoles(effects);
            var isExempt = exemptRoles.Contains(request.User.Role, StringComparer.OrdinalIgnoreCase);

            // Apply exclusions: drop excluded fields from columns and rows.
            var keptColumns = result.Columns
                .Where(c => !excluded.Contains(c))
                .ToList();
            var keptRows = result.Rows
                .Select(row => row
                    .Where(kv => !excluded.Contains(kv.Key))
                    .ToDictionary(kv => kv.Key, kv => kv.Value))
                .ToList();

            // Apply masking for non-exempt requesters.
            IReadOnlyList<IReadOnlyDictionary<string, string?>> finalRows;
            if (!isExempt && masked.Count > 0)
            {
                finalRows = keptRows.Select(row =>
                {
                    var maskedRow = new Dictionary<string, string?>();
                    foreach (var kv in row)
                    {
                        maskedRow[kv.Key] = masked.Contains(kv.Key) ? "***" : kv.Value;
                    }
                    return maskedRow as IReadOnlyDictionary<string, string?>;
                }).ToList();
            }
            else
            {
                finalRows = keptRows;
            }

            // Apply row cap.
            var cap = BusinessRuleEngine.CapRows(effects);
            if (cap is int capped && capped >= 0 && finalRows.Count > capped)
            {
                finalRows = finalRows.Take(capped).ToList();
            }

            result = result with { Columns = keptColumns, Rows = finalRows };
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
