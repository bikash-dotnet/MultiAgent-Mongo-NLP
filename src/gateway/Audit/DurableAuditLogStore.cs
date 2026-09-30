using Gateway.Persistence;

namespace Gateway.Audit;

public sealed class DurableAuditLogStore : IAuditLogStore
{
    private const string Collection = "audit_logs";

    private readonly IDocumentStore _store;

    public DurableAuditLogStore(IDocumentStore store)
    {
        _store = store;
    }

    public async Task<AuditLogDocument> AppendAsync(AuditLogDocument document, CancellationToken cancellationToken = default)
    {
        var stored = string.IsNullOrWhiteSpace(document.Id)
            ? document with { Id = Guid.NewGuid().ToString("N") }
            : document;

        await _store.UpsertAsync(Collection, stored.Id, stored, cancellationToken);
        return stored;
    }

    public Task<AuditLogDocument?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        return _store.GetAsync<AuditLogDocument>(Collection, id, cancellationToken);
    }

    public Task<IReadOnlyList<AuditLogDocument>> ListAsync(CancellationToken cancellationToken = default)
    {
        return _store.GetAllAsync<AuditLogDocument>(Collection, cancellationToken);
    }
}
