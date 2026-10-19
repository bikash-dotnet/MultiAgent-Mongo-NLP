namespace Gateway.Mcp;

public interface IMcpHost
{
    McpHealth GetHealth(string id);

    DateTimeOffset? LastCheckedAt(string id);

    string? LastError(string id);

    Task<McpTestResult> TestAsync(McpServerDefinition server, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(McpServerDefinition server, CancellationToken cancellationToken = default);
}

public interface IMcpSession : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DiscoveredMcpTool>> ListToolsAsync(CancellationToken cancellationToken = default);
}

public interface IMcpSessionFactory
{
    Task<IMcpSession> OpenAsync(McpServerDefinition server, CancellationToken cancellationToken = default);
}

public interface IMcpSecretProbe
{
    bool IsConfigured(string envKey);
}

public sealed class EnvironmentMcpSecretProbe : IMcpSecretProbe
{
    public bool IsConfigured(string envKey) =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(envKey));
}
