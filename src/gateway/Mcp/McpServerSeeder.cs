namespace Gateway.Mcp;

public static class McpServerSeeder
{
    public const string PocServerId = "mcp_mongo_ai_query";

    public static McpServerDefinition BuildPocDefinition(DateTimeOffset? now = null)
    {
        var stamp = now ?? DateTimeOffset.UtcNow;
        return new McpServerDefinition(
            PocServerId,
            "mongo-ai-query",
            "Sample POC MCP server (stdio). Not part of the production solution and can be removed.",
            McpTransport.Stdio,
            "node",
            ["src/poc/mcp-server.js.js"],
            null,
            ["MONGO_URI", "API_KEY"],
            false,
            ["get_schema", "generate_pipeline", "run_pipeline", "ask"],
            true,
            5000,
            McpScope.Global,
            null,
            1,
            stamp,
            "system");
    }
}
