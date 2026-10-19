namespace Gateway.Mcp;

public static class McpToolClassifier
{
    private static readonly HashSet<string> ReadOnlyTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "get_schema",
        "generate_pipeline",
        "run_pipeline",
        "ask"
    };

    private static readonly string[] WriteTokens =
    [
        "write", "delete", "drop", "update", "insert", "create", "admin",
        "exec", "execute_write", "kill", "mutate", "put", "post", "patch",
        "remove", "truncate", "grant", "revoke"
    ];

    public static bool IsReadOnly(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return false;
        }

        if (ReadOnlyTools.Contains(toolName))
        {
            return true;
        }

        foreach (var token in WriteTokens)
        {
            if (toolName.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return toolName.StartsWith("get_", StringComparison.OrdinalIgnoreCase)
            || toolName.StartsWith("list_", StringComparison.OrdinalIgnoreCase)
            || toolName.StartsWith("read_", StringComparison.OrdinalIgnoreCase)
            || toolName.StartsWith("describe_", StringComparison.OrdinalIgnoreCase)
            || toolName.StartsWith("search_", StringComparison.OrdinalIgnoreCase);
    }
}
