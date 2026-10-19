using MongoDB.Bson;
using MongoDB.Driver;

namespace Gateway.Persistence;

public sealed class MongoDocumentStore : IDocumentStore
{
    private readonly IMongoDatabase _database;

    public MongoDocumentStore(string connectionString, string database)
    {
        MongoBsonConfiguration.EnsureRegistered();
        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(database);
    }

    public async Task<T?> GetAsync<T>(string collection, string id, CancellationToken cancellationToken = default)
    {
        return await Collection<T>(collection)
            .Find(Builders<T>.Filter.Eq("_id", id))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<T>> GetAllAsync<T>(string collection, CancellationToken cancellationToken = default)
    {
        return await Collection<T>(collection)
            .Find(Builders<T>.Filter.Empty)
            .ToListAsync(cancellationToken);
    }

    public Task UpsertAsync<T>(string collection, string id, T document, CancellationToken cancellationToken = default)
    {
        return Collection<T>(collection).ReplaceOneAsync(
            Builders<T>.Filter.Eq("_id", id),
            document,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task<bool> DeleteAsync(string collection, string id, CancellationToken cancellationToken = default)
    {
        var result = await _database
            .GetCollection<BsonDocument>(collection)
            .DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", id), cancellationToken);
        return result.DeletedCount > 0;
    }

    private IMongoCollection<T> Collection<T>(string collection) => _database.GetCollection<T>(collection);
}
