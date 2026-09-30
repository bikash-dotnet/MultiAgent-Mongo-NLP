namespace Gateway.Audit;

public interface IAuditLogStore
{
    Task<AuditLogDocument> AppendAsync(AuditLogDocument document, CancellationToken cancellationToken = default);

    Task<AuditLogDocument?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLogDocument>> ListAsync(CancellationToken cancellationToken = default);
}
