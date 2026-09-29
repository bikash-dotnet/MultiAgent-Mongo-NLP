using Gateway.Nlp.Guardrails;

namespace Gateway.Persistence;

public sealed class DurableAccessRequestStore : IAccessRequestStore
{
    private const string Collection = "access_requests";

    private readonly IDocumentStore _store;

    public DurableAccessRequestStore(IDocumentStore store)
    {
        _store = store;
    }

    public async Task<AccessRequest> CreateAsync(AccessRequest request, CancellationToken cancellationToken = default)
    {
        var stored = string.IsNullOrWhiteSpace(request.Id)
            ? request with { Id = Guid.NewGuid().ToString("N") }
            : request;

        await _store.UpsertAsync(Collection, stored.Id, stored, cancellationToken);
        return stored;
    }

    public Task<AccessRequest?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        return _store.GetAsync<AccessRequest>(Collection, id, cancellationToken);
    }

    public Task<AccessRequest> UpdateAsync(AccessRequest request, CancellationToken cancellationToken = default)
    {
        return UpsertAndReturn(request, cancellationToken);
    }

    public Task<IReadOnlyList<AccessRequest>> ListAsync(CancellationToken cancellationToken = default)
    {
        return _store.GetAllAsync<AccessRequest>(Collection, cancellationToken);
    }

    private async Task<AccessRequest> UpsertAndReturn(AccessRequest request, CancellationToken cancellationToken)
    {
        await _store.UpsertAsync(Collection, request.Id, request, cancellationToken);
        return request;
    }
}
