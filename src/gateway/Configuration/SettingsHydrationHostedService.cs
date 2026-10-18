using Microsoft.Extensions.Hosting;

namespace Gateway.Configuration;

public sealed class SettingsHydrationHostedService : IHostedService
{
    private readonly ISettingsStore _store;
    private readonly ISettingsProvider _provider;
    private readonly ILogger<SettingsHydrationHostedService> _logger;

    public SettingsHydrationHostedService(
        ISettingsStore store,
        ISettingsProvider provider,
        ILogger<SettingsHydrationHostedService> logger)
    {
        _store = store;
        _provider = provider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            foreach (var doc in await _store.ListAsync(cancellationToken))
            {
                var values = doc.ValuesAsElement();
                _provider.UpdateSection(doc.Id, values, doc.Version);
            }

            _logger.LogInformation("Hydrated {Count} configuration sections from the store.", _provider.GetSectionNames().Count);
        }
        catch (Exception error)
        {
            _logger.LogWarning(error, "Failed to hydrate configuration settings from the store; defaults will apply.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
