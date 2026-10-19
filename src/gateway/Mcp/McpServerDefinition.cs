using System.Text.Json.Serialization;

namespace Gateway.Mcp;

[JsonConverter(typeof(JsonStringEnumConverter<McpTransport>))]
public enum McpTransport
{
    Stdio,
    Http
}

[JsonConverter(typeof(JsonStringEnumConverter<McpScope>))]
public enum McpScope
{
    Global,
    Schema,
    Role
}

public sealed record McpServerDefinition(
    string Id,
    string Name,
    string? Description,
    McpTransport Transport,
    string? Command,
    IReadOnlyList<string> Args,
    string? Url,
    IReadOnlyList<string> EnvKeys,
    bool Enabled,
    IReadOnlyList<string> AllowedTools,
    bool ReadOnly,
    int TimeoutMs,
    McpScope Scope,
    string? ScopeValue,
    int Version,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy)
{
    public static string NewId() => "mcp_" + Guid.NewGuid().ToString("N")[..12];
}
