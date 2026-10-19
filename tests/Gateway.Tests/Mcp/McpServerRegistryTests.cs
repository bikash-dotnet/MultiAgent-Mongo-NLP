using Gateway.Mcp;
using Gateway.Persistence;

namespace Gateway.Tests.Mcp;

public class McpServerRegistryTests
{
    private readonly InMemoryDocumentStore _backend = new();
    private readonly McpServerRegistry _registry;

    public McpServerRegistryTests()
    {
        _registry = new McpServerRegistry(_backend);
    }

    [Fact]
    public async Task Seed_inserts_disabled_poc_server()
    {
        await _registry.SeedAsync();

        var loaded = await _registry.GetAsync(McpServerSeeder.PocServerId);

        Assert.NotNull(loaded);
        Assert.False(loaded!.Enabled);
        Assert.Equal("mongo-ai-query", loaded.Name);
        Assert.Equal(McpTransport.Stdio, loaded.Transport);
        Assert.Equal("node", loaded.Command);
        Assert.Equal(["src/poc/mcp-server.js.js"], loaded.Args);
        Assert.Equal(["MONGO_URI", "API_KEY"], loaded.EnvKeys);
        Assert.Equal(["get_schema", "generate_pipeline", "run_pipeline", "ask"], loaded.AllowedTools);
        Assert.True(loaded.ReadOnly);
        Assert.Equal(5000, loaded.TimeoutMs);
        Assert.Equal(McpScope.Global, loaded.Scope);
        Assert.Null(loaded.ScopeValue);
        Assert.Contains("sample", loaded.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("remove", loaded.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Seed_is_idempotent()
    {
        await _registry.SeedAsync();
        await _registry.SeedAsync();

        var all = await _registry.ListAsync();
        Assert.Single(all);
        Assert.Equal(1, all[0].Version);
    }

    [Fact]
    public async Task Seed_does_not_overwrite_operator_edited_definition()
    {
        await _registry.SeedAsync();
        var original = await _registry.GetAsync(McpServerSeeder.PocServerId);
        Assert.NotNull(original);

        var edited = await _registry.SaveAsync(
            original! with { Name = "operator-edited", TimeoutMs = 9000 },
            "operator-1");

        await _registry.SeedAsync();

        var loaded = await _registry.GetAsync(McpServerSeeder.PocServerId);
        Assert.NotNull(loaded);
        Assert.Equal("operator-edited", loaded!.Name);
        Assert.Equal(9000, loaded.TimeoutMs);
        Assert.Equal(edited.Version, loaded.Version);
        Assert.Equal("operator-1", loaded.UpdatedBy);
    }

    [Fact]
    public async Task Create_then_get_round_trips_with_version()
    {
        var created = await _registry.CreateAsync(NewServer("mcp_custom"), "user-1");

        Assert.Equal(1, created.Version);
        Assert.Equal("user-1", created.UpdatedBy);
        Assert.NotNull(created.UpdatedAt);

        var loaded = await _registry.GetAsync("mcp_custom");
        Assert.NotNull(loaded);
        Assert.Equal("custom", loaded!.Name);
        Assert.Equal(1, loaded.Version);
        Assert.Equal(["MONGO_URI"], loaded.EnvKeys);
    }

    [Fact]
    public async Task Save_increments_version()
    {
        var created = await _registry.CreateAsync(NewServer("mcp_v"), "user-1");
        var saved = await _registry.SaveAsync(created with { Name = "renamed" }, "user-2");

        Assert.Equal(2, saved.Version);
        Assert.Equal("user-2", saved.UpdatedBy);
        Assert.Equal("renamed", saved.Name);

        var loaded = await _registry.GetAsync("mcp_v");
        Assert.Equal(2, loaded!.Version);
    }

    [Fact]
    public async Task Save_rejects_stale_version()
    {
        var created = await _registry.CreateAsync(NewServer("mcp_stale"), "user-1");
        await _registry.SaveAsync(created with { Name = "first-write" }, "user-2");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _registry.SaveAsync(created with { Name = "stale-write" }, "user-3"));
    }

    [Fact]
    public async Task Enable_and_disable_toggle_and_increment_version()
    {
        var created = await _registry.CreateAsync(NewServer("mcp_toggle"), "user-1");
        Assert.False(created.Enabled);

        var enabled = await _registry.EnableAsync("mcp_toggle", "admin");
        Assert.True(enabled.Enabled);
        Assert.Equal(2, enabled.Version);
        Assert.Equal("admin", enabled.UpdatedBy);

        var disabled = await _registry.DisableAsync("mcp_toggle", "admin");
        Assert.False(disabled.Enabled);
        Assert.Equal(3, disabled.Version);
    }

    [Fact]
    public async Task Enable_unknown_server_throws()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _registry.EnableAsync("missing", "admin"));
    }

    [Fact]
    public async Task List_returns_all_servers()
    {
        await _registry.CreateAsync(NewServer("mcp_a"), "user-1");
        await _registry.CreateAsync(NewServer("mcp_b") with { Name = "b" }, "user-1");

        var all = await _registry.ListAsync();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task Delete_removes_server()
    {
        await _registry.CreateAsync(NewServer("mcp_delete"), "user-1");

        var deleted = await _registry.DeleteAsync("mcp_delete");

        Assert.True(deleted);
        Assert.Null(await _registry.GetAsync("mcp_delete"));
        Assert.False(await _registry.DeleteAsync("mcp_delete"));
    }

    [Fact]
    public async Task Poc_seed_env_keys_are_names_only()
    {
        var seed = McpServerSeeder.BuildPocDefinition();

        Assert.All(seed.EnvKeys, key =>
        {
            Assert.False(string.IsNullOrWhiteSpace(key));
            Assert.DoesNotContain('=', key);
            Assert.DoesNotContain("mongodb://", key, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("sk-", key, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Null(seed.Url);
        Assert.False(seed.Enabled);
    }

    private static McpServerDefinition NewServer(string id) => new(
        id,
        "custom",
        "test server",
        McpTransport.Stdio,
        "node",
        ["src/poc/mcp-server.js.js"],
        null,
        ["MONGO_URI"],
        false,
        ["get_schema"],
        true,
        5000,
        McpScope.Global,
        null,
        1,
        null,
        null);
}
