namespace Gateway.Mcp;

public sealed class McpServerSeedHostedService : IHostedService
{
    private readonly IMcpServerRegistry _registry;
    private readonly ILogger<McpServerSeedHostedService> _logger;

    public McpServerSeedHostedService(
        IMcpServerRegistry registry,
        ILogger<McpServerSeedHostedService> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _registry.SeedAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MCP server seed on startup failed; continuing without a seeded definition.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
