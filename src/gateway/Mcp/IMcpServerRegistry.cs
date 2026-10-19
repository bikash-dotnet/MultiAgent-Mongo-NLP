namespace Gateway.Mcp;

public interface IMcpServerRegistry
{
    Task<IReadOnlyList<McpServerDefinition>> ListAsync(CancellationToken cancellationToken = default);

    Task<McpServerDefinition?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task<McpServerDefinition> CreateAsync(McpServerDefinition server, string updatedBy,
        CancellationToken cancellationToken = default);

    Task<McpServerDefinition> SaveAsync(McpServerDefinition server, string updatedBy,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task<McpServerDefinition> EnableAsync(string id, string updatedBy,
        CancellationToken cancellationToken = default);

    Task<McpServerDefinition> DisableAsync(string id, string updatedBy,
        CancellationToken cancellationToken = default);

    Task SeedAsync(CancellationToken cancellationToken = default);
}
