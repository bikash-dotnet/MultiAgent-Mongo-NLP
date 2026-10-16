using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Gateway.Schemas;

public sealed class SchemaRegistrySeedHostedService : IHostedService
{
    private readonly ISchemaRegistry _registry;
    private readonly ILogger<SchemaRegistrySeedHostedService> _logger;

    public SchemaRegistrySeedHostedService(ISchemaRegistry registry, ILogger<SchemaRegistrySeedHostedService> logger)
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
            _logger.LogWarning(ex, "Schema registry seed on startup failed; continuing without a seeded schema.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
