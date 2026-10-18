using Gateway.Persistence;

namespace Gateway.Schemas;

public sealed class SchemaRegistryStore : ISchemaRegistry
{
    private const string Collection = "schemas";

    private readonly IDocumentStore _store;
    private readonly SemaphoreSlim _activationGate = new(1, 1);

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
        await _activationGate.WaitAsync(cancellationToken);
        var previous = new Dictionary<string, bool>(StringComparer.Ordinal);

        try
        {
            var all = await ListAsync(cancellationToken);
            var target = all.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal));
            if (target is null)
            {
                throw new KeyNotFoundException($"Schema '{id}' was not found.");
            }

            foreach (var schema in all)
            {
                previous[schema.Id] = schema.IsActiveSource;
            }

            var now = DateTimeOffset.UtcNow;

            // Activate the target first so a mid-flight failure never leaves zero active sources.
            await _store.UpsertAsync(
                Collection,
                target.Id,
                target with { IsActiveSource = true, UpdatedAt = now },
                cancellationToken);

            foreach (var schema in all)
            {
                if (string.Equals(schema.Id, id, StringComparison.Ordinal) || !schema.IsActiveSource)
                {
                    continue;
                }

                await _store.UpsertAsync(
                    Collection,
                    schema.Id,
                    schema with { IsActiveSource = false, UpdatedAt = now },
                    cancellationToken);
            }
        }
        catch
        {
            await TryRestoreAsync(previous);
            throw;
        }
        finally
        {
            _activationGate.Release();
        }
    }

    private async Task TryRestoreAsync(IReadOnlyDictionary<string, bool> previous)
    {
        if (previous.Count == 0)
        {
            return;
        }

        try
        {
            var current = await ListAsync(CancellationToken.None);
            foreach (var schema in current)
            {
                if (!previous.TryGetValue(schema.Id, out var wasActive) || wasActive == schema.IsActiveSource)
                {
                    continue;
                }

                await _store.UpsertAsync(
                    Collection,
                    schema.Id,
                    schema with { IsActiveSource = wasActive },
                    CancellationToken.None);
            }
        }
        catch
        {
            // Best-effort rollback; the original failure is more actionable to the caller.
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
