using System.Collections.Concurrent;
using System.Text.Json;

namespace Gateway.Persistence;

public sealed class InMemoryDocumentStore : IDocumentStore
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _collections = new(StringComparer.Ordinal);

    public Task<T?> GetAsync<T>(string collection, string id, CancellationToken cancellationToken = default)
    {
        if (!_collections.TryGetValue(collection, out var documents) || !documents.TryGetValue(id, out var json))
        {
            return Task.FromResult<T?>(default);
        }

        return Task.FromResult(JsonSerializer.Deserialize<T>(json));
    }

    public Task<IReadOnlyList<T>> GetAllAsync<T>(string collection, CancellationToken cancellationToken = default)
    {
        if (!_collections.TryGetValue(collection, out var documents))
        {
            return Task.FromResult<IReadOnlyList<T>>([]);
        }

        IReadOnlyList<T> all = documents.Values
            .Select(json => JsonSerializer.Deserialize<T>(json)!)
            .ToList();

        return Task.FromResult(all);
    }

    public Task UpsertAsync<T>(string collection, string id, T document, CancellationToken cancellationToken = default)
    {
        var documents = _collections.GetOrAdd(collection, _ => new ConcurrentDictionary<string, string>(StringComparer.Ordinal));
        documents[id] = JsonSerializer.Serialize(document);
        return Task.CompletedTask;
    }
}
