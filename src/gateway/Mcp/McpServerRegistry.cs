using Gateway.Persistence;

namespace Gateway.Mcp;

public sealed class McpServerRegistry : IMcpServerRegistry
{
    public const string Collection = "mcp_servers";

    private readonly IDocumentStore _store;

    public McpServerRegistry(IDocumentStore store)
    {
        _store = store;
    }

    public Task<IReadOnlyList<McpServerDefinition>> ListAsync(CancellationToken cancellationToken = default) =>
        _store.GetAllAsync<McpServerDefinition>(Collection, cancellationToken);

    public Task<McpServerDefinition?> GetAsync(string id, CancellationToken cancellationToken = default) =>
        _store.GetAsync<McpServerDefinition>(Collection, id, cancellationToken);

    public async Task<McpServerDefinition> CreateAsync(McpServerDefinition server, string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var id = string.IsNullOrWhiteSpace(server.Id) ? McpServerDefinition.NewId() : server.Id;
        var stored = Stamp(server with { Id = id, Version = 1 }, updatedBy);
        await _store.UpsertAsync(Collection, stored.Id, stored, cancellationToken);
        return stored;
    }

    public async Task<McpServerDefinition> SaveAsync(McpServerDefinition server, string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var current = await _store.GetAsync<McpServerDefinition>(Collection, server.Id, cancellationToken);
        if (current is null)
        {
            throw new KeyNotFoundException($"MCP server '{server.Id}' was not found.");
        }

        if (server.Version != current.Version)
        {
            throw new InvalidOperationException(
                $"MCP server '{server.Id}' has version {current.Version}; expected {server.Version}.");
        }

        var stored = Stamp(server with { Version = current.Version + 1 }, updatedBy);
        await _store.UpsertAsync(Collection, stored.Id, stored, cancellationToken);
        return stored;
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default) =>
        _store.DeleteAsync(Collection, id, cancellationToken);

    public Task<McpServerDefinition> EnableAsync(string id, string updatedBy,
        CancellationToken cancellationToken = default) =>
        SetEnabledAsync(id, true, updatedBy, cancellationToken);

    public Task<McpServerDefinition> DisableAsync(string id, string updatedBy,
        CancellationToken cancellationToken = default) =>
        SetEnabledAsync(id, false, updatedBy, cancellationToken);

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(McpServerSeeder.PocServerId, cancellationToken);
        if (existing is not null)
        {
            return;
        }

        var seed = McpServerSeeder.BuildPocDefinition();
        await _store.UpsertAsync(Collection, seed.Id, seed, cancellationToken);
    }

    private async Task<McpServerDefinition> SetEnabledAsync(string id, bool enabled, string updatedBy,
        CancellationToken cancellationToken)
    {
        var current = await GetAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"MCP server '{id}' was not found.");

        var stored = Stamp(current with { Enabled = enabled, Version = current.Version + 1 }, updatedBy);
        await _store.UpsertAsync(Collection, stored.Id, stored, cancellationToken);
        return stored;
    }

    private static McpServerDefinition Stamp(McpServerDefinition server, string updatedBy) =>
        server with { UpdatedAt = DateTimeOffset.UtcNow, UpdatedBy = updatedBy };
}
