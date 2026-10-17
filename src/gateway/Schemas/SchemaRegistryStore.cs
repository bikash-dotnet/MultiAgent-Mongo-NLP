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
        return all.FirstOrDefault(schema => schema.IsActiveSource);
    }

    public async Task<SchemaDefinition> CreateAsync(SchemaDefinition schema, CancellationToken cancellationToken = default)
    {
        var stored = string.IsNullOrWhiteSpace(schema.Id) ? schema with { Id = SchemaDefinition.NewId() } : schema;
        await _store.UpsertAsync(Collection, stored.Id, stored, cancellationToken);
        return stored;
    }

    public async Task<SchemaDefinition> SaveAsync(SchemaDefinition schema, CancellationToken cancellationToken = default)
    {
        var current = await _store.GetAsync<SchemaDefinition>(Collection, schema.Id, cancellationToken);
        var stored = current is null
            ? schema with { Version = 1 }
            : schema with { Version = current.Version + 1 };

        await _store.UpsertAsync(Collection, stored.Id, stored, cancellationToken);
        return stored;
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        return _store.DeleteAsync(Collection, id, cancellationToken);
    }

    public async Task ActivateAsync(string id, CancellationToken cancellationToken = default)
    {
        var all = await ListAsync(cancellationToken);
        var target = all.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal));
        if (target is null)
        {
            throw new KeyNotFoundException($"Schema '{id}' was not found.");
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var schema in all)
        {
            var next = string.Equals(schema.Id, id, StringComparison.Ordinal)
                ? schema with { IsActiveSource = true, UpdatedAt = now }
                : schema with { IsActiveSource = false, UpdatedAt = now };

            if (next.IsActiveSource != schema.IsActiveSource || next.UpdatedAt != schema.UpdatedAt)
            {
                await _store.UpsertAsync(Collection, next.Id, next, cancellationToken);
            }
        }
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var existing = await ListAsync(cancellationToken);
        if (existing.Count > 0)
        {
            return;
        }

        var schema = SchemaSeeder.BuildDefault();
        await _store.UpsertAsync(Collection, schema.Id, schema, cancellationToken);
    }
}
