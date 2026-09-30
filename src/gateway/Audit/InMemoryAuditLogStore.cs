using System.Collections.Concurrent;

namespace Gateway.Audit;

public sealed class InMemoryAuditLogStore : IAuditLogStore
{
    private readonly ConcurrentDictionary<string, AuditLogDocument> _documents = new(StringComparer.Ordinal);

    public Task<AuditLogDocument> AppendAsync(AuditLogDocument document, CancellationToken cancellationToken = default)
    {
        var stored = string.IsNullOrWhiteSpace(document.Id)
            ? document with { Id = Guid.NewGuid().ToString("N") }
            : document;

        _documents[stored.Id] = stored;
        return Task.FromResult(stored);
    }

    public Task<AuditLogDocument?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        _documents.TryGetValue(id, out var document);
        return Task.FromResult(document);
    }

    public Task<IReadOnlyList<AuditLogDocument>> ListAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<AuditLogDocument>>(_documents.Values.ToList());
    }
}
