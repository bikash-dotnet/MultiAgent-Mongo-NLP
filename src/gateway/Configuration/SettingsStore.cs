using System.Text.Json;

using Gateway.Persistence;

namespace Gateway.Configuration;
public sealed class SettingsStore : ISettingsStore
{
    private const string Collection = "app_settings";
    private readonly IDocumentStore _store;

    public SettingsStore(IDocumentStore store)
    {
        _store = store;
    }

    public Task<SettingsDocument?> GetAsync(string sectionKey, CancellationToken cancellationToken = default) =>
        _store.GetAsync<SettingsDocument>(Collection, sectionKey, cancellationToken);

    public Task<IReadOnlyList<SettingsDocument>> ListAsync(CancellationToken cancellationToken = default) =>
        _store.GetAllAsync<SettingsDocument>(Collection, cancellationToken);

    public async Task<SettingsDocument> UpsertAsync(string sectionKey, JsonElement values, string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var current = await _store.GetAsync<SettingsDocument>(Collection, sectionKey, cancellationToken);
        var version = current is null ? 1 : current.Version + 1;

        var doc = new SettingsDocument(sectionKey, JsonSerializer.Serialize(values), version, SettingsSource.Database, now, updatedBy);
        await _store.UpsertAsync(Collection, sectionKey, doc, cancellationToken);
        return doc;
    }

    public Task<bool> DeleteAsync(string sectionKey, CancellationToken cancellationToken = default) =>
        _store.DeleteAsync(Collection, sectionKey, cancellationToken);

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        foreach (var doc in await ListAsync(cancellationToken))
        {
            await DeleteAsync(doc.Id, cancellationToken);
        }
    }
}
