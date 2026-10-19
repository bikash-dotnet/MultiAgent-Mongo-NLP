using Gateway.Mcp;

namespace Gateway.Tests.Mcp;

public class McpHostTests
{
    [Fact]
    public async Task Test_initialize_and_list_tools_against_stub_session()
    {
        var factory = new StubMcpSessionFactory
        {
            Tools =
            [
                new DiscoveredMcpTool("get_schema", "Return schema", "{}"),
                new DiscoveredMcpTool("ask", "Ask a question", "{}")
            ]
        };
        var host = new McpHost(factory);
        var server = EnabledServer();

        var result = await host.TestAsync(server, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Equal(McpHealth.Healthy, result.Health);
        Assert.Equal(2, result.Tools.Count);
        Assert.Contains(result.Tools, t => t.Name == "get_schema" && t.Allowed);
        Assert.True(result.LatencyMs >= 0);
        Assert.Equal(1, factory.OpenCalls);
    }

    [Fact]
    public async Task Test_times_out_when_session_exceeds_server_timeout()
    {
        var factory = new StubMcpSessionFactory { Delay = TimeSpan.FromSeconds(2) };
        var host = new McpHost(factory);
        var server = EnabledServer() with { TimeoutMs = 50 };

        var result = await host.TestAsync(server, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(McpHealth.Down, result.Health);
        Assert.Contains("timeout", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Test_retries_with_backoff_then_succeeds()
    {
        var factory = new StubMcpSessionFactory { FailuresBeforeSuccess = 2 };
        var host = new McpHost(factory);

        var result = await host.TestAsync(EnabledServer(), CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Equal(3, factory.OpenCalls);
    }

    [Fact]
    public async Task Circuit_breaker_skips_dead_server()
    {
        var factory = new StubMcpSessionFactory { FailuresBeforeSuccess = int.MaxValue };
        var host = new McpHost(factory, failureThreshold: 3, openDuration: TimeSpan.FromMinutes(1));
        var server = EnabledServer();

        for (var i = 0; i < 3; i++)
        {
            var down = await host.TestAsync(server, CancellationToken.None);
            Assert.False(down.Ok);
        }

        var blocked = await host.TestAsync(server, CancellationToken.None);
        Assert.False(blocked.Ok);
        Assert.Equal(McpHealth.Down, blocked.Health);
        Assert.Contains("circuit", blocked.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, factory.OpenCalls);
    }

    [Fact]
    public async Task Disabled_server_is_not_started()
    {
        var factory = new StubMcpSessionFactory();
        var host = new McpHost(factory);
        var server = EnabledServer() with { Enabled = false };

        var result = await host.TestAsync(server, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(McpHealth.Disabled, result.Health);
        Assert.Equal(0, factory.OpenCalls);
        Assert.Equal(McpHealth.Disabled, host.GetHealth(server.Id));
    }

    [Fact]
    public async Task ListTools_marks_allow_list_and_read_only()
    {
        var factory = new StubMcpSessionFactory
        {
            Tools =
            [
                new DiscoveredMcpTool("get_schema", "schema", "{}"),
                new DiscoveredMcpTool("delete_document", "write", "{}"),
                new DiscoveredMcpTool("ask", "ask", "{}")
            ]
        };
        var host = new McpHost(factory);
        var server = EnabledServer() with { AllowedTools = ["get_schema"] };

        var tools = await host.ListToolsAsync(server, CancellationToken.None);

        Assert.True(tools.Single(t => t.Name == "get_schema").Allowed);
        Assert.False(tools.Single(t => t.Name == "ask").Allowed);
        Assert.False(tools.Single(t => t.Name == "delete_document").ReadOnly);
        Assert.True(tools.Single(t => t.Name == "get_schema").ReadOnly);
    }

    private sealed class StubMcpSessionFactory : IMcpSessionFactory
    {
        public int OpenCalls { get; private set; }
        public TimeSpan Delay { get; set; }
        public int FailuresBeforeSuccess { get; set; }
        public IReadOnlyList<DiscoveredMcpTool> Tools { get; set; } =
            [new DiscoveredMcpTool("get_schema", "schema", "{}")];

        public async Task<IMcpSession> OpenAsync(McpServerDefinition server, CancellationToken cancellationToken = default)
        {
            OpenCalls++;
            if (Delay > TimeSpan.Zero)
            {
                throw new TimeoutException("stub timeout");
            }

            if (OpenCalls <= FailuresBeforeSuccess)
            {
                throw new InvalidOperationException("stub failure");
            }

            return new StubMcpSession(Tools);
        }
    }

    private sealed class StubMcpSession : IMcpSession
    {
        private readonly IReadOnlyList<DiscoveredMcpTool> _tools;

        public StubMcpSession(IReadOnlyList<DiscoveredMcpTool> tools)
        {
            _tools = tools;
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<DiscoveredMcpTool>> ListToolsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_tools);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static McpServerDefinition EnabledServer() => new(
        "mcp_stub",
        "stub",
        null,
        McpTransport.Stdio,
        "node",
        ["stub.js"],
        null,
        ["MONGO_URI"],
        true,
        ["get_schema"],
        true,
        5000,
        McpScope.Global,
        null,
        1,
        null,
        null);
}
