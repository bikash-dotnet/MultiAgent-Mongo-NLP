using System.Security.Claims;
using System.Text.Json;
using Gateway.Audit;
using Gateway.Auth;
using Gateway.Handlers;
using Gateway.Mcp;
using Gateway.Persistence;
using Microsoft.AspNetCore.Http;

namespace Gateway.Tests.Mcp;

public class McpServerHandlersTests
{
    private readonly InMemoryDocumentStore _backend = new();
    private readonly McpServerRegistry _registry;
    private readonly InMemoryAuditLogStore _audit = new();
    private readonly StubMcpHost _host = new();
    private readonly TestEnvironment _env = new();

    public McpServerHandlersTests()
    {
        _registry = new McpServerRegistry(_backend);
    }

    [Fact]
    public async Task Create_returns_stored_server_and_writes_audit()
    {
        var result = await McpServerHandlers.Create(
            NewRequest(), AdminUser(), _registry, _audit, CancellationToken.None);

        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<McpServerDefinition>>(result);
        Assert.Equal(1, ok.Value!.Version);
        Assert.Equal("admin-1", ok.Value.UpdatedBy);
        Assert.False(ok.Value.Enabled);

        var logs = await _audit.ListAsync();
        Assert.Contains("mcp_server.create", logs.Select(l => l.SchemaAction));
    }

    [Fact]
    public async Task List_returns_summaries_with_health_and_tool_count()
    {
        await McpServerHandlers.Create(NewRequest(), AdminUser(), _registry, _audit, CancellationToken.None);
        _host.HealthById["mcp_test"] = McpHealth.Down;

        var result = await McpServerHandlers.List(_registry, _host, CancellationToken.None);
        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<IReadOnlyList<McpServerSummary>>>(result);
        var summary = Assert.Single(ok.Value!);
        Assert.Equal("mcp_test", summary.Id);
        Assert.Equal(McpTransport.Stdio, summary.Transport);
        Assert.Equal(McpHealth.Disabled, summary.Health);
        Assert.Equal(1, summary.ToolCount);
    }

    [Fact]
    public async Task Get_redacts_secrets_and_reports_env_key_configuration()
    {
        Environment.SetEnvironmentVariable("MONGO_URI", "mongodb://should-never-appear");
        try
        {
            await McpServerHandlers.Create(NewRequest(), AdminUser(), _registry, _audit, CancellationToken.None);

            var result = await McpServerHandlers.Get("mcp_test", _registry, _host, _env, CancellationToken.None);
            var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<McpServerDetail>>(result);
            var json = JsonSerializer.Serialize(ok.Value);

            Assert.DoesNotContain("mongodb://should-never-appear", json, StringComparison.Ordinal);
            Assert.DoesNotContain("sk-secret", json, StringComparison.Ordinal);
            var mongo = Assert.Single(ok.Value!.EnvKeys, k => k.Name == "MONGO_URI");
            Assert.True(mongo.Configured);
            var apiKey = Assert.Single(ok.Value.EnvKeys, k => k.Name == "API_KEY");
            Assert.False(apiKey.Configured);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MONGO_URI", null);
        }
    }

    [Fact]
    public async Task Get_missing_returns_not_found()
    {
        var result = await McpServerHandlers.Get("missing", _registry, _host, _env, CancellationToken.None);
        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(404, status.StatusCode);
    }

    [Fact]
    public async Task Update_requires_matching_version()
    {
        var created = await McpServerHandlers.Create(NewRequest(), AdminUser(), _registry, _audit, CancellationToken.None);
        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<McpServerDefinition>>(created);
        var request = NewRequest();
        request.Name = "renamed";
        request.Version = ok.Value!.Version;

        var updated = await McpServerHandlers.Update(
            "mcp_test", request, AdminUser(), _registry, _audit, CancellationToken.None);
        var ok2 = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<McpServerDefinition>>(updated);
        Assert.Equal(2, ok2.Value!.Version);
        Assert.Equal("renamed", ok2.Value.Name);

        request.Name = "stale";
        request.Version = 1;
        var stale = await McpServerHandlers.Update(
            "mcp_test", request, AdminUser(), _registry, _audit, CancellationToken.None);
        var conflict = Assert.IsAssignableFrom<IStatusCodeHttpResult>(stale);
        Assert.Equal(409, conflict.StatusCode);
    }

    [Fact]
    public async Task Create_rejects_write_tool_allow_list()
    {
        var request = NewRequest();
        request.AllowedTools = ["delete_collection"];

        var result = await McpServerHandlers.Create(request, AdminUser(), _registry, _audit, CancellationToken.None);
        var bad = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(400, bad.StatusCode);
    }

    [Fact]
    public async Task Create_rejects_shell_metacharacters()
    {
        var request = NewRequest();
        request.Args = ["server.js; rm -rf /"];

        var result = await McpServerHandlers.Create(request, AdminUser(), _registry, _audit, CancellationToken.None);
        var bad = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(400, bad.StatusCode);
    }

    [Fact]
    public async Task Enable_disable_toggle_and_audit()
    {
        await McpServerHandlers.Create(NewRequest(), AdminUser(), _registry, _audit, CancellationToken.None);

        var enabled = await McpServerHandlers.Enable("mcp_test", AdminUser(), _registry, _audit, CancellationToken.None);
        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<McpServerDefinition>>(enabled);
        Assert.True(ok.Value!.Enabled);

        var disabled = await McpServerHandlers.Disable("mcp_test", AdminUser(), _registry, _audit, CancellationToken.None);
        var ok2 = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<McpServerDefinition>>(disabled);
        Assert.False(ok2.Value!.Enabled);

        var logs = await _audit.ListAsync();
        Assert.Contains("mcp_server.enable", logs.Select(l => l.SchemaAction));
        Assert.Contains("mcp_server.disable", logs.Select(l => l.SchemaAction));
    }

    [Fact]
    public async Task Delete_removes_server_and_audits()
    {
        await McpServerHandlers.Create(NewRequest(), AdminUser(), _registry, _audit, CancellationToken.None);
        var result = await McpServerHandlers.Delete("mcp_test", AdminUser(), _registry, _audit, CancellationToken.None);
        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.NoContent>(result);
        Assert.Null(await _registry.GetAsync("mcp_test"));

        var logs = await _audit.ListAsync();
        Assert.Contains("mcp_server.delete", logs.Select(l => l.SchemaAction));
    }

    [Fact]
    public async Task Test_returns_host_result_and_audits_invocation()
    {
        await McpServerHandlers.Create(NewRequest(), AdminUser(), _registry, _audit, CancellationToken.None);
        _host.TestResult = new McpTestResult(
            true, McpHealth.Healthy, 12,
            [new McpToolDescriptor("get_schema", "schema", "{}", true, true)],
            null);

        var result = await McpServerHandlers.Test(
            "mcp_test", AdminUser(), _registry, _host, _audit, CancellationToken.None);
        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<McpTestResult>>(result);
        Assert.True(ok.Value!.Ok);
        Assert.Equal(12, ok.Value.LatencyMs);

        var logs = await _audit.ListAsync();
        Assert.Contains("mcp_server.test", logs.Select(l => l.SchemaAction));
    }

    [Fact]
    public async Task Tools_returns_discovered_tools_with_allow_list_status()
    {
        await McpServerHandlers.Create(NewRequest(), AdminUser(), _registry, _audit, CancellationToken.None);
        _host.Tools =
        [
            new McpToolDescriptor("get_schema", "schema", "{}", true, true),
            new McpToolDescriptor("ask", "ask", "{}", false, true)
        ];

        var result = await McpServerHandlers.Tools("mcp_test", _registry, _host, CancellationToken.None);
        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<IReadOnlyList<McpToolDescriptor>>>(result);
        Assert.Equal(2, ok.Value!.Count);
        Assert.True(ok.Value[0].Allowed);
        Assert.False(ok.Value[1].Allowed);
    }

    [Fact]
    public async Task Responses_never_contain_secret_values()
    {
        Environment.SetEnvironmentVariable("API_KEY", "sk-super-secret-value");
        try
        {
            var request = NewRequest();
            request.EnvKeys = ["API_KEY"];
            await McpServerHandlers.Create(request, AdminUser(), _registry, _audit, CancellationToken.None);

            var get = await McpServerHandlers.Get("mcp_test", _registry, _host, _env, CancellationToken.None);
            var list = await McpServerHandlers.List(_registry, _host, CancellationToken.None);
            var json = JsonSerializer.Serialize(get) + JsonSerializer.Serialize(list);

            Assert.DoesNotContain("sk-super-secret-value", json, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("API_KEY", null);
        }
    }

    private static ClaimsPrincipal AdminUser() =>
        new(new ClaimsIdentity(
        [
            new Claim(SessionClaims.UserId, "admin-1"),
            new Claim(SessionClaims.Name, "Admin"),
            new Claim(SessionClaims.Role, "Data Owner / Admin")
        ]));

    private static McpServerRequest NewRequest() => new()
    {
        Id = "mcp_test",
        Name = "test",
        Description = "unit test server",
        Transport = "Stdio",
        Command = "node",
        Args = ["src/poc/mcp-server.js.js"],
        EnvKeys = ["MONGO_URI", "API_KEY"],
        Enabled = false,
        AllowedTools = ["get_schema"],
        ReadOnly = true,
        TimeoutMs = 5000,
        Scope = "Global"
    };

    private sealed class TestEnvironment : IMcpSecretProbe
    {
        public bool IsConfigured(string envKey) =>
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(envKey));
    }

    private sealed class StubMcpHost : IMcpHost
    {
        public Dictionary<string, McpHealth> HealthById { get; } = new(StringComparer.Ordinal);
        public McpTestResult TestResult { get; set; } = new(true, McpHealth.Healthy, 0, [], null);
        public IReadOnlyList<McpToolDescriptor> Tools { get; set; } = [];

        public McpHealth GetHealth(string id) =>
            HealthById.TryGetValue(id, out var health) ? health : McpHealth.Down;

        public DateTimeOffset? LastCheckedAt(string id) => null;

        public string? LastError(string id) => null;

        public Task<McpTestResult> TestAsync(McpServerDefinition server, CancellationToken cancellationToken = default) =>
            Task.FromResult(TestResult);

        public Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(McpServerDefinition server, CancellationToken cancellationToken = default) =>
            Task.FromResult(Tools);
    }
}
