using Gateway.Nlp;

namespace Gateway.Schemas;

public sealed class SchemaRegistrySeedHostedService : IHostedService
{
    private readonly ISchemaRegistry _registry;
    private readonly ActiveSchemaProvider _provider;
    private readonly ILogger<SchemaRegistrySeedHostedService> _logger;

    public SchemaRegistrySeedHostedService(
        ISchemaRegistry registry,
        ActiveSchemaProvider provider,
        ILogger<SchemaRegistrySeedHostedService> logger)
    {
        _registry = registry;
        _provider = provider;
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
            _logger.LogWarning(ex, "Schema registry seed on startup failed; continuing without a seeded schema.");
        }

        try
        {
            await _provider.RefreshAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Active schema refresh on startup failed; guardrails will fail open until it succeeds.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
