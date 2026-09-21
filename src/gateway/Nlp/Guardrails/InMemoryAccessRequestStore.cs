using System.Collections.Concurrent;

namespace Gateway.Nlp.Guardrails;

public sealed class InMemoryAccessRequestStore : IAccessRequestStore
{
    private readonly ConcurrentDictionary<string, AccessRequest> _requests = new(StringComparer.Ordinal);

    public Task<AccessRequest> CreateAsync(AccessRequest request, CancellationToken cancellationToken = default)
    {
        var stored = string.IsNullOrWhiteSpace(request.Id)
            ? request with { Id = Guid.NewGuid().ToString("N") }
            : request;

        _requests[stored.Id] = stored;
        return Task.FromResult(stored);
    }

    public Task<AccessRequest?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        _requests.TryGetValue(id, out var request);
        return Task.FromResult(request);
    }

    public Task<AccessRequest> UpdateAsync(AccessRequest request, CancellationToken cancellationToken = default)
    {
        _requests[request.Id] = request;
        return Task.FromResult(request);
    }
}
