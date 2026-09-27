using Gateway.Nlp.Guardrails;

namespace Gateway.Governance;

public interface IGovernanceService
{
    Task<AccessRequest> CreatePendingAsync(AccessRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccessRequest>> ListAsync(RequesterContext viewer, string? status = null, CancellationToken cancellationToken = default);

    Task<AccessRequest?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task<AccessRequest> GetForViewerAsync(string id, RequesterContext viewer, CancellationToken cancellationToken = default);

    Task<AccessRequest> ApproveAsync(string id, RequesterContext actor, string? notes, CancellationToken cancellationToken = default);

    Task<AccessRequest> RejectAsync(string id, RequesterContext actor, string? notes, CancellationToken cancellationToken = default);

    Task<AccessRequest> OverrideAsync(string id, RequesterContext actor, string notes, CancellationToken cancellationToken = default);

    bool IsDataOwner(RequesterContext context);
}
