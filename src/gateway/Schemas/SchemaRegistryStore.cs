using Gateway.Persistence;

namespace Gateway.Schemas;

public sealed class SchemaRegistryStore : ISchemaRegistry
{
    private const string Collection = "schemas";

    private readonly IDocumentStore _store;

    public SchemaRegistryStore(IDocumentStore store)
    {
        _store = store;
    }

    public Task<IReadOnlyList<SchemaDefinition>> ListAsync(CancellationToken cancellationToken = default)
    {
        return _store.GetAllAsync<SchemaDefinition>(Collection, cancellationToken);
    }

    public Task<SchemaDefinition?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        return _store.GetAsync<SchemaDefinition>(Collection, id, cancellationToken);
    }

    public async Task<SchemaDefinition?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var all = await ListAsync(cancellationToken);
        return all.FirstOrDefault(schema => schema.IsActiveSource) ?? all.FirstOrDefault();
    }

    public async Task<SchemaDefinition> CreateAsync(SchemaDefinition schema, CancellationToken cancellationToken = default)
    {
        var stored = string.IsNullOrWhiteSpace(schema.Id) ? schema with { Id = SchemaDefinition.NewId() } : schema;
        await _store.UpsertAsync(Collection, stored.Id, stored, cancellationToken);
        return stored;
    }

    public async Task<SchemaDefinition> SaveAsync(SchemaDefinition schema, CancellationToken cancellationToken = default)
    {
        await _store.UpsertAsync(Collection, schema.Id, schema, cancellationToken);
        return schema;
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        return _store.DeleteAsync(Collection, id, cancellationToken);
    }
}
