using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;

namespace Gateway.Persistence;

public sealed class MongoDocumentStore : IDocumentStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private readonly IMongoDatabase _database;

    public MongoDocumentStore(string connectionString, string database)
    {
        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(database);
    }

    public async Task<T?> GetAsync<T>(string collection, string id, CancellationToken cancellationToken = default)
    {
        var found = await Collection(collection)
            .Find(Builders<BsonDocument>.Filter.Eq("_id", id))
            .FirstOrDefaultAsync(cancellationToken);

        return found is null ? default : Deserialize<T>(found);
    }

    public async Task<IReadOnlyList<T>> GetAllAsync<T>(string collection, CancellationToken cancellationToken = default)
    {
        var documents = await Collection(collection)
            .Find(Builders<BsonDocument>.Filter.Empty)
            .ToListAsync(cancellationToken);

        return documents.Select(Deserialize<T>).ToList();
    }

    public Task UpsertAsync<T>(string collection, string id, T document, CancellationToken cancellationToken = default)
    {
        var bson = BsonDocument.Parse(JsonSerializer.Serialize(document, Options));
        bson["_id"] = id;
        return Collection(collection).ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", id),
            bson,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    private IMongoCollection<BsonDocument> Collection(string collection)
    {
        return _database.GetCollection<BsonDocument>(collection);
    }

    private static T Deserialize<T>(BsonDocument document)
    {
        document.Remove("_id");
        var json = document.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });
        return JsonSerializer.Deserialize<T>(json, Options)!;
    }
}
