using System.Collections.Concurrent;
using System.Diagnostics;

namespace Gateway.Mcp;

public sealed class McpHost : IMcpHost
{
    private readonly IMcpSessionFactory _factory;
    private readonly int _failureThreshold;
    private readonly TimeSpan _openDuration;
    private readonly ConcurrentDictionary<string, ServerState> _states = new(StringComparer.Ordinal);

    public McpHost(IMcpSessionFactory factory, int failureThreshold = 3, TimeSpan? openDuration = null)
    {
        _factory = factory;
        _failureThreshold = Math.Max(1, failureThreshold);
        _openDuration = openDuration ?? TimeSpan.FromMinutes(1);
    }

    public McpHealth GetHealth(string id) =>
        _states.TryGetValue(id, out var state) ? state.Health : McpHealth.Down;

    public DateTimeOffset? LastCheckedAt(string id) =>
        _states.TryGetValue(id, out var state) ? state.LastCheckedAt : null;

    public string? LastError(string id) =>
        _states.TryGetValue(id, out var state) ? state.LastError : null;

    public async Task<McpTestResult> TestAsync(McpServerDefinition server, CancellationToken cancellationToken = default)
    {
        if (!server.Enabled)
        {
            Record(server.Id, McpHealth.Disabled, "Server is disabled.", resetFailures: true);
            return new McpTestResult(false, McpHealth.Disabled, 0, [], "Server is disabled.");
        }

        if (IsCircuitOpen(server.Id, out var circuitError))
        {
            return new McpTestResult(false, McpHealth.Down, 0, [], circuitError);
        }

        var timeout = TimeSpan.FromMilliseconds(Math.Max(1, server.TimeoutMs));
        var stopwatch = Stopwatch.StartNew();
        string? lastError = null;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (IsCircuitOpen(server.Id, out var openError))
            {
                stopwatch.Stop();
                return new McpTestResult(false, McpHealth.Down, stopwatch.ElapsedMilliseconds, [], openError);
            }

            if (attempt > 0)
            {
                var backoff = TimeSpan.FromMilliseconds(50 * Math.Pow(2, attempt - 1));
                try
                {
                    await Task.Delay(backoff, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(timeout);
                var tools = await OpenAndListAsync(server, timeoutCts.Token);
                stopwatch.Stop();
                var descriptors = Project(server, tools);
                Record(server.Id, McpHealth.Healthy, null, resetFailures: true);
                return new McpTestResult(true, McpHealth.Healthy, stopwatch.ElapsedMilliseconds, descriptors, null);
            }
            catch (TimeoutException ex)
            {
                stopwatch.Stop();
                Record(server.Id, McpHealth.Down, ex.Message, resetFailures: false);
                return new McpTestResult(false, McpHealth.Down, stopwatch.ElapsedMilliseconds, [], ex.Message);
            }
            catch (OperationCanceledException ex)
            {
                stopwatch.Stop();
                Record(server.Id, McpHealth.Down, ex.Message, resetFailures: false);
                return new McpTestResult(false, McpHealth.Down, stopwatch.ElapsedMilliseconds, [], ex.Message);
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                RecordFailure(server.Id, lastError);
            }
        }

        stopwatch.Stop();
        var health = GetHealth(server.Id);
        if (health != McpHealth.Down && health != McpHealth.Degraded)
        {
            health = McpHealth.Down;
        }

        return new McpTestResult(false, health, stopwatch.ElapsedMilliseconds, [], lastError ?? "MCP handshake failed.");
    }

    public async Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(McpServerDefinition server, CancellationToken cancellationToken = default)
    {
        var result = await TestAsync(server, cancellationToken);
        return result.Tools;
    }

    private async Task<IReadOnlyList<DiscoveredMcpTool>> OpenAndListAsync(McpServerDefinition server, CancellationToken cancellationToken)
    {
        await using var session = await _factory.OpenAsync(server, cancellationToken);
        await session.InitializeAsync(cancellationToken);
        return await session.ListToolsAsync(cancellationToken);
    }

    private static IReadOnlyList<McpToolDescriptor> Project(McpServerDefinition server, IReadOnlyList<DiscoveredMcpTool> tools)
    {
        var allow = new HashSet<string>(server.AllowedTools, StringComparer.OrdinalIgnoreCase);
        return tools
            .Select(tool => new McpToolDescriptor(
                tool.Name,
                tool.Description,
                tool.InputSchema,
                allow.Contains(tool.Name),
                McpToolClassifier.IsReadOnly(tool.Name)))
            .ToList();
    }

    private bool IsCircuitOpen(string id, out string? error)
    {
        error = null;
        if (!_states.TryGetValue(id, out var state))
        {
            return false;
        }

        if (state.ConsecutiveFailures < _failureThreshold)
        {
            return false;
        }

        if (DateTimeOffset.UtcNow - state.OpenedAt < _openDuration)
        {
            error = "Circuit breaker is open for this MCP server.";
            return true;
        }

        return false;
    }

    private int RecordFailure(string id, string message)
    {
        var next = _states.AddOrUpdate(
            id,
            _ => new ServerState(McpHealth.Down, DateTimeOffset.UtcNow, message, 1, DateTimeOffset.UtcNow),
            (_, current) =>
            {
                var failures = current.ConsecutiveFailures + 1;
                var health = failures >= _failureThreshold ? McpHealth.Down : McpHealth.Degraded;
                var opened = failures >= _failureThreshold ? DateTimeOffset.UtcNow : current.OpenedAt;
                return new ServerState(health, DateTimeOffset.UtcNow, message, failures, opened);
            });
        return next.ConsecutiveFailures;
    }

    private void Record(string id, McpHealth health, string? error, bool resetFailures)
    {
        _states.AddOrUpdate(
            id,
            _ => new ServerState(health, DateTimeOffset.UtcNow, error, 0, DateTimeOffset.MinValue),
            (_, current) => new ServerState(
                health,
                DateTimeOffset.UtcNow,
                error,
                resetFailures ? 0 : current.ConsecutiveFailures,
                resetFailures ? DateTimeOffset.MinValue : current.OpenedAt));
    }

    private sealed record ServerState(
        McpHealth Health,
        DateTimeOffset LastCheckedAt,
        string? LastError,
        int ConsecutiveFailures,
        DateTimeOffset OpenedAt);
}
