using Gateway.Persistence;

namespace Gateway.Schemas;

public sealed class SchemaRegistryStore : ISchemaRegistry
{
    private const string Collection = "schemas";
    private const string LockCollection = "schema_locks";
    private const string LockId = "_activation_lock";

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
        try
        {
            await ActivateCrossProcessAsync(id, cancellationToken);
        }
        finally
        {
            _activationGate.Release();
        }
    }

    private async Task ActivateCrossProcessAsync(string id, CancellationToken cancellationToken)
    {
        var token = Guid.NewGuid().ToString("N");
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);

        // Distributed lock serializes activation across gateway instances. The in-process
        // semaphore above handles the single-instance fast path without a store round trip.
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await TryAcquireLockAsync(token, cancellationToken))
            {
                try
                {
                    await ActivateLockedAsync(id, cancellationToken);
                    return;
                }
                finally
                {
                    await ReleaseLockAsync(token, cancellationToken);
                }
            }

            await Task.Delay(50, cancellationToken);
        }

        throw new TimeoutException($"Could not acquire the schema activation lock for '{id}'.");
    }

    private async Task<bool> TryAcquireLockAsync(string token, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var existing = await _store.GetAsync<ActivationLock>(LockCollection, LockId, cancellationToken);
        if (existing is not null && existing.ExpiresAt > now)
        {
            return false;
        }

        var doc = new ActivationLock(LockId, token, now.AddSeconds(30));
        await _store.UpsertAsync(LockCollection, LockId, doc, cancellationToken);

        // Upsert is not atomic, so verify we won the write before treating the lock as held.
        var verify = await _store.GetAsync<ActivationLock>(LockCollection, LockId, cancellationToken);
        return verify is not null && string.Equals(verify.Token, token, StringComparison.Ordinal);
    }

    private async Task ReleaseLockAsync(string token, CancellationToken cancellationToken)
    {
        var existing = await _store.GetAsync<ActivationLock>(LockCollection, LockId, cancellationToken);
        if (existing is not null && string.Equals(existing.Token, token, StringComparison.Ordinal))
        {
            await _store.UpsertAsync(
                LockCollection,
                LockId,
                existing with { Token = null, ExpiresAt = DateTimeOffset.UtcNow },
                cancellationToken);
        }
    }

    private async Task ActivateLockedAsync(string id, CancellationToken cancellationToken)
    {
        var all = await ListAsync(cancellationToken);
        var target = all.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal));
        if (target is null)
        {
            throw new KeyNotFoundException($"Schema '{id}' was not found.");
        }

        var previous = all.ToDictionary(schema => schema.Id, schema => schema.IsActiveSource, StringComparer.Ordinal);
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

public sealed record ActivationLock(
    string Id,
    string? Token,
    DateTimeOffset ExpiresAt);
