using Gateway.Persistence;

namespace Gateway.Rules;

public sealed class ColumnDefinitionStore : IColumnDefinitionStore
{
    private const string Collection = "column_definitions";
    private readonly IDocumentStore _store;

    public ColumnDefinitionStore(IDocumentStore store)
    {
        _store = store;
    }

    public Task<ColumnDefinition?> GetAsync(string id, CancellationToken cancellationToken = default) =>
        _store.GetAsync<ColumnDefinition>(Collection, id, cancellationToken);

    public Task<IReadOnlyList<ColumnDefinition>> ListAsync(CancellationToken cancellationToken = default) =>
        _store.GetAllAsync<ColumnDefinition>(Collection, cancellationToken);

    public async Task<ColumnDefinition> UpsertAsync(ColumnDefinition definition, string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var current = await _store.GetAsync<ColumnDefinition>(Collection, definition.Id, cancellationToken);
        var version = current is null ? 1 : current.Version + 1;

        var stored = definition
            .withId(string.IsNullOrWhiteSpace(definition.Id) ? ColumnDefinition.NewId() : definition.Id)
            .withVersion(version)
            with { UpdatedAt = now, UpdatedBy = updatedBy };

        await _store.UpsertAsync(Collection, stored.Id, stored, cancellationToken);
        return stored;
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default) =>
        _store.DeleteAsync(Collection, id, cancellationToken);
}
