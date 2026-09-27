using Gateway.Conversations;
using Gateway.Nlp.Guardrails;
using Gateway.Nlp.Orchestrator;

namespace Gateway.Governance;

public sealed class GovernanceService : IGovernanceService
{
    private readonly IAccessRequestStore _requests;
    private readonly IConversationResumeHandler _resume;
    private readonly IAgentEventSink _events;
    private readonly TimeProvider _clock;

    public GovernanceService(
        IAccessRequestStore requests,
        IConversationResumeHandler resume,
        IAgentEventSink events,
        TimeProvider clock)
    {
        _requests = requests;
        _resume = resume;
        _events = events;
        _clock = clock;
    }

    public Task<AccessRequest> CreatePendingAsync(AccessRequest request, CancellationToken cancellationToken = default)
    {
        return _requests.CreateAsync(request, cancellationToken);
    }

    public async Task<IReadOnlyList<AccessRequest>> ListAsync(RequesterContext viewer, string? status = null, CancellationToken cancellationToken = default)
    {
        var all = await _requests.ListAsync(cancellationToken);
        var scoped = Scope(all, viewer);
        return status is null
            ? scoped
            : scoped.Where(request => request.Status == status).ToList();
    }

    public Task<AccessRequest?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        return _requests.GetAsync(id, cancellationToken);
    }

    public async Task<AccessRequest> GetForViewerAsync(string id, RequesterContext viewer, CancellationToken cancellationToken = default)
    {
        var request = await Required(id, cancellationToken);
        if (!IsInScope(request, viewer))
        {
            throw new GovernanceForbiddenException("This access request is outside your scope.");
        }

        return request;
    }

    public async Task<AccessRequest> ApproveAsync(string id, RequesterContext actor, string? notes, CancellationToken cancellationToken = default)
    {
        var request = await RequiredPending(id, cancellationToken);
        var isAssignee = request.AssignedLeadId == actor.UserId;
        var canOverride = IsOverrideAuthority(actor);
        if (!isAssignee && !canOverride)
        {
            throw new GovernanceForbiddenException("Only the assigned lead or an override authority can approve this request.");
        }

        var overrideInvoked = !isAssignee;
        var overrideType = overrideInvoked ? ApprovalResolution.HierarchicalManagementOverride : null;
        return await ResolveAsync(request, actor, AccessRequest.Approved, overrideInvoked, overrideType, notes, cancellationToken);
    }

    public async Task<AccessRequest> RejectAsync(string id, RequesterContext actor, string? notes, CancellationToken cancellationToken = default)
    {
        var request = await RequiredPending(id, cancellationToken);
        var isAssignee = request.AssignedLeadId == actor.UserId;
        if (!isAssignee && !IsOverrideAuthority(actor))
        {
            throw new GovernanceForbiddenException("Only the assigned lead or an override authority can reject this request.");
        }

        return await ResolveAsync(request, actor, AccessRequest.Rejected, false, null, notes, cancellationToken);
    }

    public async Task<AccessRequest> OverrideAsync(string id, RequesterContext actor, string notes, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            throw new GovernanceValidationException("Override notes are required.");
        }

        var request = await RequiredPending(id, cancellationToken);
        if (!IsOverrideAuthority(actor))
        {
            throw new GovernanceForbiddenException("Only a manager, director, or data owner can override.");
        }

        return await ResolveAsync(
            request,
            actor,
            AccessRequest.Approved,
            true,
            ApprovalResolution.HierarchicalManagementOverride,
            notes,
            cancellationToken);
    }

    public bool IsDataOwner(RequesterContext context)
    {
        return RoleNormalizer.Matches(context.Role, "DataOwner") || RoleNormalizer.Matches(context.Role, "Admin");
    }

    private bool IsOverrideAuthority(RequesterContext context)
    {
        return IsDataOwner(context)
            || RoleNormalizer.Matches(context.Role, "Engineering Manager")
            || RoleNormalizer.Matches(context.Role, "Director");
    }

    private static bool IsTeamLead(RequesterContext context)
    {
        return RoleNormalizer.Matches(context.Role, "Team Lead");
    }

    private IReadOnlyList<AccessRequest> Scope(IReadOnlyList<AccessRequest> all, RequesterContext viewer)
    {
        if (IsDataOwner(viewer) || IsOverrideAuthority(viewer))
        {
            return all;
        }

        if (IsTeamLead(viewer))
        {
            return all.Where(request => request.AssignedLeadId == viewer.UserId).ToList();
        }

        return all.Where(request => request.Requester?.UserId == viewer.UserId).ToList();
    }

    private bool IsInScope(AccessRequest request, RequesterContext viewer)
    {
        return Scope([request], viewer).Count > 0;
    }

    private async Task<AccessRequest> Required(string id, CancellationToken cancellationToken)
    {
        return await _requests.GetAsync(id, cancellationToken) ?? throw new GovernanceNotFoundException(id);
    }

    private async Task<AccessRequest> RequiredPending(string id, CancellationToken cancellationToken)
    {
        var request = await Required(id, cancellationToken);
        if (request.Status != AccessRequest.PendingLead)
        {
            throw new GovernanceConflictException($"Access request '{id}' is {request.Status} and cannot be changed.");
        }

        return request;
    }

    private async Task<AccessRequest> ResolveAsync(
        AccessRequest request,
        RequesterContext actor,
        string status,
        bool overrideInvoked,
        string? overrideType,
        string? notes,
        CancellationToken cancellationToken)
    {
        var resolution = new ApprovalResolution(
            request.AssignedLeadId ?? actor.UserId,
            actor.UserId,
            actor.Name,
            actor.Role,
            overrideInvoked,
            overrideType,
            _clock.GetUtcNow(),
            string.IsNullOrWhiteSpace(notes) ? null : notes.Trim());

        var updated = await _requests.UpdateAsync(request with { Status = status, Resolution = resolution }, cancellationToken);

        if (status == AccessRequest.Approved)
        {
            await _resume.ResumeAsync(updated.Id, cancellationToken);
            _events.Publish(new AgentEvent("governance.approved", "approved", updated.Id));
        }
        else
        {
            _events.Publish(new AgentEvent("governance.rejected", "rejected", updated.Id));
        }

        return updated;
    }
}
