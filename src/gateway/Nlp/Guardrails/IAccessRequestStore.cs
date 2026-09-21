namespace Gateway.Nlp.Guardrails;

public interface IAccessRequestStore
{
    Task<AccessRequest> CreateAsync(AccessRequest request, CancellationToken cancellationToken = default);

    Task<AccessRequest?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task<AccessRequest> UpdateAsync(AccessRequest request, CancellationToken cancellationToken = default);
}
