using System.Text.Json.Serialization;

namespace Gateway.Mcp;

[JsonConverter(typeof(JsonStringEnumConverter<McpHealth>))]
public enum McpHealth
{
    Healthy,
    Degraded,
    Down,
    Disabled
}

public sealed record McpServerSummary(
    string Id,
    string Name,
    McpTransport Transport,
    McpScope Scope,
    bool Enabled,
    McpHealth Health,
    int ToolCount,
    DateTimeOffset? UpdatedAt);

public sealed record McpEnvKeyStatus(string Name, bool Configured);

public sealed record McpServerDetail(
    string Id,
    string Name,
    string? Description,
    McpTransport Transport,
    string? Command,
    IReadOnlyList<string> Args,
    string? Url,
    IReadOnlyList<McpEnvKeyStatus> EnvKeys,
    bool Enabled,
    IReadOnlyList<string> AllowedTools,
    bool ReadOnly,
    int TimeoutMs,
    McpScope Scope,
    string? ScopeValue,
    int Version,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    McpHealth Health,
    DateTimeOffset? LastCheckedAt,
    string? LastError);

public sealed record McpToolDescriptor(
    string Name,
    string? Description,
    string? InputSchema,
    bool Allowed,
    bool ReadOnly);

public sealed record McpTestResult(
    bool Ok,
    McpHealth Health,
    long LatencyMs,
    IReadOnlyList<McpToolDescriptor> Tools,
    string? Error);

public sealed record DiscoveredMcpTool(string Name, string? Description, string? InputSchema);
