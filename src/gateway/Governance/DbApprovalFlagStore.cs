using Gateway.Configuration;
using System.Text.Json;

namespace Gateway.Governance;

public sealed class DbApprovalFlagStore : IApprovalFlagStore
{
    private readonly ISettingsStore _store;
    private readonly ISettingsProvider _provider;

    public DbApprovalFlagStore(ISettingsStore store, ISettingsProvider provider)
    {
        _store = store;
        _provider = provider;
    }

    public bool Enabled =>
        _provider.TryGetValue("Governance", "ApprovalEnabled", out var value) &&
        (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            ? value.GetBoolean()
            : true;

    public async Task<bool> SetAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        var values = JsonSerializer.SerializeToElement(new Dictionary<string, bool>
        {
            ["ApprovalEnabled"] = enabled
        });

        var doc = await _store.UpsertAsync("Governance", values, "system", cancellationToken);
        var root = JsonElement.Parse(doc.ValuesJson).Clone();
        _provider.UpdateSection("Governance", root, doc.Version);
        return enabled;
    }
}
