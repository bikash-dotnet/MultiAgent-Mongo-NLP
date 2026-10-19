namespace Gateway.Handlers;

public sealed class McpServerRequest
{
    public string? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Transport { get; set; } = "Stdio";
    public string? Command { get; set; }
    public IReadOnlyList<string> Args { get; set; } = [];
    public string? Url { get; set; }
    public IReadOnlyList<string> EnvKeys { get; set; } = [];
    public bool Enabled { get; set; }
    public IReadOnlyList<string> AllowedTools { get; set; } = [];
    public bool ReadOnly { get; set; } = true;
    public int TimeoutMs { get; set; } = 5000;
    public string Scope { get; set; } = "Global";
    public string? ScopeValue { get; set; }
    public int Version { get; set; }
}
