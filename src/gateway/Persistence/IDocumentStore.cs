namespace Gateway.Persistence;

public interface IDocumentStore
{
    Task<T?> GetAsync<T>(string collection, string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> GetAllAsync<T>(string collection, CancellationToken cancellationToken = default);

    Task UpsertAsync<T>(string collection, string id, T document, CancellationToken cancellationToken = default);
}
