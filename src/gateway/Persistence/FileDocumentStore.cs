using System.Text.Json;

namespace Gateway.Persistence;

public sealed class FileDocumentStore : IDocumentStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private readonly string _directory;

    public FileDocumentStore(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(_directory);
    }

    public async Task<T?> GetAsync<T>(string collection, string id, CancellationToken cancellationToken = default)
    {
        var path = PathFor(collection, id);
        if (!File.Exists(path))
        {
            return default;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<T>(json, Options);
    }

    public async Task<IReadOnlyList<T>> GetAllAsync<T>(string collection, CancellationToken cancellationToken = default)
    {
        var directory = CollectionDirectory(collection);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var results = new List<T>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var document = JsonSerializer.Deserialize<T>(json, Options);
            if (document is not null)
            {
                results.Add(document);
            }
        }

        return results;
    }

    public async Task UpsertAsync<T>(string collection, string id, T document, CancellationToken cancellationToken = default)
    {
        var directory = CollectionDirectory(collection);
        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(document, Options);
        await File.WriteAllTextAsync(PathFor(collection, id), json, cancellationToken);
    }

    private string CollectionDirectory(string collection)
    {
        return Path.Combine(_directory, collection);
    }

    private string PathFor(string collection, string id)
    {
        return Path.Combine(CollectionDirectory(collection), $"{id}.json");
    }
}
