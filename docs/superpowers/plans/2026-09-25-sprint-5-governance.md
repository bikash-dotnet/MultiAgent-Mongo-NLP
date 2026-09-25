# Sprint 5 Governance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete hierarchical governance in the gateway and SPA: Data Owner exemption, Team Lead approval queue that resumes paused reports, Engineering Manager/Director override, and an Angular governance portal.

**Architecture:** Keep `AccessRequest` in `Gateway.Nlp.Guardrails` and add a thin `IGovernanceService` seam in `Gateway.Governance` that owns exemption checks, role-scoped queue queries, the approve/reject/override lifecycle, and the resume hand-off. Requester identity flows from `ClaimsPrincipal` as a `RequesterContext` into the NLP and conversation orchestrators. A tiny `IConversationResumeHandler` (implemented by `ConversationOrchestrator`) lifts the conversation hold after approval. All stores stay in-memory.

**Tech Stack:** ASP.NET Core 10 minimal API, xUnit, in-memory stores; Angular 21 standalone components, RxJS, vitest + jsdom.

## Global Constraints

- Runtime floor: `net10.0` for the gateway and tests; do not change the target framework.
- Do not add any NuGet or npm package.
- Enums and statuses are strings on the wire (`"PENDING_LEAD"`, `"APPROVED"`, `"REJECTED"`, `"EMAIL"`, `"CSV"`).
- Existing positional records only gain trailing optional parameters with defaults; never reorder or add a required parameter.
- Governance decisions fail closed and are authorized server-side from the JWT; the client is never trusted.
- All persistence is in-memory: `IAccessRequestStore`, `IConversationStore`, `IAgentStateStore`. No MongoDB.
- No code comments and no emojis anywhere.
- Never commit a real API key; keep `NvidiaNim:ApiKey` as the existing `TBD` placeholder.
- Do not modify CI. Do not commit `src/web/.vscode/`.
- Running the full .NET suite rewrites `docs/benchmarks/*.md`; always `git restore docs/benchmarks/` and never stage those files.
- Commit messages must use the repo hook workaround (`printf ... > /tmp/msg` then `git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg`) and end with the `Co-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>` trailer.
- Prefix every shell invocation that needs .NET with `export PATH="$PATH:/root/.dotnet"`.

---

## File Structure

New backend files:

- `src/gateway/Governance/RequesterContext.cs` — requester identity.
- `src/gateway/Governance/RoleNormalizer.cs` — role token normalization and matching.
- `src/gateway/Governance/IGovernanceService.cs` — governance seam interface.
- `src/gateway/Governance/GovernanceService.cs` — exemption, queue, lifecycle, resume hand-off.
- `src/gateway/Governance/GovernanceExceptions.cs` — typed failures mapped to HTTP status.
- `src/gateway/Nlp/Guardrails/RequesterInfo.cs`, `RequestedFlag.cs`, `GovernanceJustification.cs`, `ApprovalResolution.cs` — request detail records.
- `src/gateway/Conversations/IConversationResumeHandler.cs` — resume hand-off interface.

New SPA files:

- `src/web/src/app/models/governance.ts` — portal models.
- `src/web/src/app/services/governance.service.ts` (+ spec) — access-request API.
- `src/web/src/app/governance/governance-queue.component.ts` / `.html` / `.scss` / `.spec.ts` — portal page.
- `src/web/src/app/workspace/workspace.component.ts` / `.html` / `.scss` / `.spec.ts` — the existing shell extracted for routing.

New test files: `tests/Gateway.Tests/Governance/RoleNormalizerTests.cs`, `GovernanceServiceTests.cs`, `tests/Gateway.Tests/Nlp/GuardrailEvaluatorExemptionTests.cs`, `NlpOrchestratorExemptionTests.cs`, `tests/Gateway.Tests/Conversations/ConversationResumeTests.cs`, `tests/Gateway.Tests/ApiGovernanceTests.cs`.

Changed backend files: `AccessRequest.cs`, `GuardrailResult.cs`, `GuardrailEvaluator.cs`, `AgentState.cs`, `INlpOrchestrator.cs`, `NlpOrchestrator.cs`, `ConversationStep.cs`, `ConversationControl.cs`, `ConversationAnswer.cs`, `ReportIntakeDraft.cs`, `IConversationStore.cs`, `InMemoryConversationStore.cs`, `ConversationOrchestrator.cs`, `IAccessRequestStore.cs`, `InMemoryAccessRequestStore.cs`, `SessionClaims.cs`, `NlpServiceCollectionExtensions.cs`, `Program.cs`.

---

### Task 1: Requester context and role normalization

**Files:**
- Create: `src/gateway/Governance/RequesterContext.cs`
- Create: `src/gateway/Governance/RoleNormalizer.cs`
- Modify: `src/gateway/Auth/SessionClaims.cs`
- Test: `tests/Gateway.Tests/Governance/RoleNormalizerTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `Gateway.Governance.RequesterContext(string UserId, string Name, string Role, string? LeadUserId)`
  - `Gateway.Governance.RoleNormalizer.Normalize(string) : string`
  - `Gateway.Governance.RoleNormalizer.Matches(string claimRole, string ownerRole) : bool`
  - `Gateway.Auth.SessionClaims.ToRequesterContext(ClaimsPrincipal) : RequesterContext`
  - `SessionClaims.Roles` includes `"Director"`.

- [ ] **Step 1: Write the failing test**

Create `tests/Gateway.Tests/Governance/RoleNormalizerTests.cs`:

```csharp
using Gateway.Governance;

namespace Gateway.Tests.Governance;

public class RoleNormalizerTests
{
    [Theory]
    [InlineData("Data Owner / Admin", "DataOwner")]
    [InlineData("Data Owner / Admin", "Admin")]
    [InlineData("dataowner", "DataOwner")]
    public void Matches_owner_roles(string claim, string owner)
    {
        Assert.True(RoleNormalizer.Matches(claim, owner));
    }

    [Theory]
    [InlineData("Team Lead", "DataOwner")]
    [InlineData("Business Analyst", "Admin")]
    [InlineData("Engineering Manager", "Director")]
    [InlineData("", "Admin")]
    public void Rejects_non_matching_roles(string claim, string owner)
    {
        Assert.False(RoleNormalizer.Matches(claim, owner));
    }

    [Fact]
    public void Normalize_strips_spaces_and_punctuation()
    {
        Assert.Equal("dataowneradmin", RoleNormalizer.Normalize("Data Owner / Admin"));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~RoleNormalizerTests"
```

Expected: FAIL to compile because `RoleNormalizer` does not exist.

- [ ] **Step 3: Create the role normalizer**

Create `src/gateway/Governance/RoleNormalizer.cs`:

```csharp
namespace Gateway.Governance;

public static class RoleNormalizer
{
    public static string Normalize(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return string.Empty;
        }

        return new string(role.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }

    public static bool Matches(string claimRole, string ownerRole)
    {
        var claim = Normalize(claimRole);
        var owner = Normalize(ownerRole);
        if (claim.Length == 0 || owner.Length == 0)
        {
            return false;
        }

        return claim.Contains(owner, StringComparison.Ordinal) || owner.Contains(claim, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 4: Create the requester context record**

Create `src/gateway/Governance/RequesterContext.cs`:

```csharp
namespace Gateway.Governance;

public sealed record RequesterContext(string UserId, string Name, string Role, string? LeadUserId);
```

- [ ] **Step 5: Add the role list entry and the claims helper**

Modify `src/gateway/Auth/SessionClaims.cs`. Add `using Gateway.Governance;` after `using System.Security.Claims;`, add `"Director"` to the `Roles` array, and add this method after `DisplayName`:

```csharp
    public static RequesterContext ToRequesterContext(ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(UserId) ?? "anonymous";
        var role = user.FindFirstValue(Role) ?? string.Empty;
        var leadUserId = user.FindFirstValue(LeadUserId);

        return new RequesterContext(
            userId,
            DisplayName(user),
            role,
            string.IsNullOrWhiteSpace(leadUserId) ? null : leadUserId);
    }
```

- [ ] **Step 6: Run the test to verify it passes**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~RoleNormalizerTests"
```

Expected: PASS, 6 tests.

- [ ] **Step 7: Commit**

```bash
cd /workspace
git add src/gateway/Governance/RequesterContext.cs src/gateway/Governance/RoleNormalizer.cs src/gateway/Auth/SessionClaims.cs tests/Gateway.Tests/Governance/RoleNormalizerTests.cs
printf 'feat(governance): add requester context and role normalization\n\nCo-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>\n' > /tmp/msg
git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg
```

---

### Task 2: Access request details and the governance service

**Files:**
- Create: `src/gateway/Nlp/Guardrails/RequesterInfo.cs`
- Create: `src/gateway/Nlp/Guardrails/RequestedFlag.cs`
- Create: `src/gateway/Nlp/Guardrails/GovernanceJustification.cs`
- Create: `src/gateway/Nlp/Guardrails/ApprovalResolution.cs`
- Create: `src/gateway/Conversations/IConversationResumeHandler.cs`
- Create: `src/gateway/Governance/GovernanceExceptions.cs`
- Create: `src/gateway/Governance/IGovernanceService.cs`
- Create: `src/gateway/Governance/GovernanceService.cs`
- Modify: `src/gateway/Nlp/Guardrails/AccessRequest.cs`
- Modify: `src/gateway/Nlp/Guardrails/IAccessRequestStore.cs`
- Modify: `src/gateway/Nlp/Guardrails/InMemoryAccessRequestStore.cs`
- Test: `tests/Gateway.Tests/Governance/GovernanceServiceTests.cs`

**Interfaces:**
- Consumes: `RequesterContext`, `RoleNormalizer` (Task 1).
- Produces:
  - `RequesterInfo`, `RequestedFlag`, `GovernanceJustification`, `ApprovalResolution` records.
  - `AccessRequest` trailing fields `Requester`, `AssignedLeadId`, `RequestedFlags`, `JustificationDetails`, `Resolution`; constants `Approved`, `Rejected`, `ExemptionOwnerAccess`.
  - `IAccessRequestStore.ListAsync(CancellationToken)`.
  - `IConversationResumeHandler.ResumeAsync(string accessRequestId, CancellationToken)`.
  - `IGovernanceService` with `CreatePendingAsync`, `ListAsync`, `GetAsync`, `GetForViewerAsync`, `ApproveAsync`, `RejectAsync`, `OverrideAsync`, `IsDataOwner`.

- [ ] **Step 1: Write the failing test**

Create `tests/Gateway.Tests/Governance/GovernanceServiceTests.cs`:

```csharp
using Gateway.Conversations;
using Gateway.Governance;
using Gateway.Nlp.Guardrails;
using Gateway.Nlp.Orchestrator;

namespace Gateway.Tests.Governance;

public class GovernanceServiceTests
{
    private sealed class StubResumeHandler : IConversationResumeHandler
    {
        public List<string> Resumed { get; } = [];

        public Task<bool> ResumeAsync(string accessRequestId, CancellationToken cancellationToken = default)
        {
            Resumed.Add(accessRequestId);
            return Task.FromResult(true);
        }
    }

    private static (GovernanceService Sut, InMemoryAccessRequestStore Store, StubResumeHandler Resume) Build()
    {
        var store = new InMemoryAccessRequestStore();
        var resume = new StubResumeHandler();
        var sut = new GovernanceService(store, resume, new InMemoryAgentEventSink(), TimeProvider.System);
        return (sut, store, resume);
    }

    private static AccessRequest Pending(string id, string lead = "usr_lead", string requester = "usr_analyst") => new(
        id,
        "sess_1",
        "[]",
        ["address.location.coordinates"],
        AccessRequest.PendingLead,
        null,
        DateTimeOffset.UnixEpoch,
        Requester: new RequesterInfo(requester, "Analyst", "Business Analyst", lead),
        AssignedLeadId: lead);

    private static RequesterContext Analyst() => new("usr_analyst", "Analyst", "Business Analyst", "usr_lead");
    private static RequesterContext Lead() => new("usr_lead", "Lead", "Team Lead", "usr_dir");
    private static RequesterContext Manager() => new("usr_mgr", "Manager", "Engineering Manager", null);
    private static RequesterContext Owner() => new("usr_owner", "Owner", "Data Owner / Admin", null);

    [Fact]
    public async Task Analyst_sees_only_own_requests()
    {
        var (sut, store, _) = Build();
        await store.CreateAsync(Pending("req_1"));
        await store.CreateAsync(Pending("req_2", requester: "usr_other"));

        var results = await sut.ListAsync(Analyst());

        Assert.Single(results);
        Assert.Equal("req_1", results[0].Id);
    }

    [Fact]
    public async Task Team_lead_sees_only_assigned_requests()
    {
        var (sut, store, _) = Build();
        await store.CreateAsync(Pending("req_1"));
        await store.CreateAsync(Pending("req_2", lead: "usr_other"));

        var results = await sut.ListAsync(Lead());

        Assert.Single(results);
        Assert.Equal("req_1", results[0].Id);
    }

    [Fact]
    public async Task Manager_and_owner_see_all_pending()
    {
        var (sut, store, _) = Build();
        await store.CreateAsync(Pending("req_1"));
        await store.CreateAsync(Pending("req_2", lead: "usr_other"));

        Assert.Equal(2, (await sut.ListAsync(Manager())).Count);
        Assert.Equal(2, (await sut.ListAsync(Owner())).Count);
    }

    [Fact]
    public async Task Assigned_lead_approves_without_override()
    {
        var (sut, store, resume) = Build();
        await store.CreateAsync(Pending("req_1"));

        var updated = await sut.ApproveAsync("req_1", Lead(), "looks good");

        Assert.Equal(AccessRequest.Approved, updated.Status);
        Assert.False(updated.Resolution!.OverrideInvoked);
        Assert.Null(updated.Resolution.OverrideType);
        Assert.Equal("usr_lead", updated.Resolution.ResolvedByUserId);
        Assert.Equal(["req_1"], resume.Resumed);
    }

    [Fact]
    public async Task Manager_approving_someone_elses_queue_records_an_override()
    {
        var (sut, store, _) = Build();
        await store.CreateAsync(Pending("req_1"));

        var updated = await sut.ApproveAsync("req_1", Manager(), "urgent");

        Assert.Equal(AccessRequest.Approved, updated.Status);
        Assert.True(updated.Resolution!.OverrideInvoked);
        Assert.Equal(ApprovalResolution.HierarchicalManagementOverride, updated.Resolution.OverrideType);
    }

    [Fact]
    public async Task Explicit_override_requires_notes()
    {
        var (sut, store, _) = Build();
        await store.CreateAsync(Pending("req_1"));

        await Assert.ThrowsAsync<GovernanceValidationException>(() => sut.OverrideAsync("req_1", Manager(), "  "));
    }

    [Fact]
    public async Task Override_by_manager_sets_tags_and_resolves()
    {
        var (sut, store, resume) = Build();
        await store.CreateAsync(Pending("req_1"));

        var updated = await sut.OverrideAsync("req_1", Manager(), "unblock today's review");

        Assert.Equal(AccessRequest.Approved, updated.Status);
        Assert.True(updated.Resolution!.OverrideInvoked);
        Assert.Equal(ApprovalResolution.HierarchicalManagementOverride, updated.Resolution.OverrideType);
        Assert.Equal("unblock today's review", updated.Resolution.Notes);
        Assert.Equal(["req_1"], resume.Resumed);
    }

    [Fact]
    public async Task Analyst_cannot_approve()
    {
        var (sut, store, _) = Build();
        await store.CreateAsync(Pending("req_1"));

        await Assert.ThrowsAsync<GovernanceForbiddenException>(() => sut.ApproveAsync("req_1", Analyst(), null));
    }

    [Fact]
    public async Task Resolving_a_resolved_request_conflicts()
    {
        var (sut, store, _) = Build();
        await store.CreateAsync(Pending("req_1"));
        await sut.ApproveAsync("req_1", Lead(), null);

        await Assert.ThrowsAsync<GovernanceConflictException>(() => sut.RejectAsync("req_1", Lead(), null));
    }

    [Fact]
    public async Task Reject_is_terminal_and_does_not_resume()
    {
        var (sut, store, resume) = Build();
        await store.CreateAsync(Pending("req_1"));

        var updated = await sut.RejectAsync("req_1", Lead(), "not needed");

        Assert.Equal(AccessRequest.Rejected, updated.Status);
        Assert.Empty(resume.Resumed);
    }

    [Fact]
    public async Task Missing_request_is_not_found()
    {
        var (sut, _, _) = Build();

        await Assert.ThrowsAsync<GovernanceNotFoundException>(() => sut.ApproveAsync("nope", Lead(), null));
    }

    [Fact]
    public void Owner_detection_uses_normalized_roles()
    {
        var (sut, _, _) = Build();

        Assert.True(sut.IsDataOwner(Owner()));
        Assert.False(sut.IsDataOwner(Manager()));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~GovernanceServiceTests"
```

Expected: FAIL to compile because the governance types do not exist.

- [ ] **Step 3: Add the request detail records**

Create `src/gateway/Nlp/Guardrails/RequesterInfo.cs`:

```csharp
namespace Gateway.Nlp.Guardrails;

public sealed record RequesterInfo(string UserId, string Name, string Role, string? LeadUserId);
```

Create `src/gateway/Nlp/Guardrails/RequestedFlag.cs`:

```csharp
namespace Gateway.Nlp.Guardrails;

public sealed record RequestedFlag(string FieldPath, string Flag);
```

Create `src/gateway/Nlp/Guardrails/GovernanceJustification.cs`:

```csharp
namespace Gateway.Nlp.Guardrails;

public sealed record GovernanceJustification(
    string BusinessReason,
    string BusinessImpact,
    string? ProjectCode);
```

Create `src/gateway/Nlp/Guardrails/ApprovalResolution.cs`:

```csharp
namespace Gateway.Nlp.Guardrails;

public sealed record ApprovalResolution(
    string AssignedLeadId,
    string ResolvedByUserId,
    string ResolvedByName,
    string ResolvedByRole,
    bool OverrideInvoked,
    string? OverrideType,
    DateTimeOffset ResolvedAt,
    string? Notes)
{
    public const string HierarchicalManagementOverride = "HIERARCHICAL_MANAGEMENT_OVERRIDE";
}
```

- [ ] **Step 4: Extend the access request and its store**

Replace `src/gateway/Nlp/Guardrails/AccessRequest.cs` with:

```csharp
namespace Gateway.Nlp.Guardrails;

public sealed record AccessRequest(
    string Id,
    string SessionId,
    string Mql,
    IReadOnlyList<string> SensitiveFields,
    string Status,
    string? Justification,
    DateTimeOffset CreatedAt,
    ReportIntake? Intake = null,
    RequesterInfo? Requester = null,
    string? AssignedLeadId = null,
    IReadOnlyList<RequestedFlag>? RequestedFlags = null,
    GovernanceJustification? JustificationDetails = null,
    ApprovalResolution? Resolution = null)
{
    public const string PendingLead = "PENDING_LEAD";

    public const string Approved = "APPROVED";

    public const string Rejected = "REJECTED";

    public const string ExemptionOwnerAccess = "EXEMPTION_OWNER_ACCESS";
}
```

Replace `src/gateway/Nlp/Guardrails/IAccessRequestStore.cs` with:

```csharp
namespace Gateway.Nlp.Guardrails;

public interface IAccessRequestStore
{
    Task<AccessRequest> CreateAsync(AccessRequest request, CancellationToken cancellationToken = default);

    Task<AccessRequest?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task<AccessRequest> UpdateAsync(AccessRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccessRequest>> ListAsync(CancellationToken cancellationToken = default);
}
```

Add `ListAsync` to `src/gateway/Nlp/Guardrails/InMemoryAccessRequestStore.cs` after `UpdateAsync`:

```csharp
    public Task<IReadOnlyList<AccessRequest>> ListAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<AccessRequest>>(_requests.Values.ToList());
    }
```

- [ ] **Step 5: Add the resume hand-off interface**

Create `src/gateway/Conversations/IConversationResumeHandler.cs`:

```csharp
namespace Gateway.Conversations;

public interface IConversationResumeHandler
{
    Task<bool> ResumeAsync(string accessRequestId, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 6: Add the typed exceptions**

Create `src/gateway/Governance/GovernanceExceptions.cs`:

```csharp
namespace Gateway.Governance;

public sealed class GovernanceNotFoundException(string id)
    : Exception($"Access request '{id}' was not found.");

public sealed class GovernanceForbiddenException(string message) : Exception(message);

public sealed class GovernanceConflictException(string message) : Exception(message);

public sealed class GovernanceValidationException(string message) : Exception(message);
```

- [ ] **Step 7: Add the service interface and implementation**

Create `src/gateway/Governance/IGovernanceService.cs`:

```csharp
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
```

Create `src/gateway/Governance/GovernanceService.cs`:

```csharp
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
```

- [ ] **Step 8: Run the test to verify it passes**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~GovernanceServiceTests"
```

Expected: PASS, 12 tests.

- [ ] **Step 9: Commit**

```bash
cd /workspace
git add src/gateway/Nlp/Guardrails src/gateway/Conversations/IConversationResumeHandler.cs src/gateway/Governance tests/Gateway.Tests/Governance/GovernanceServiceTests.cs
printf 'feat(governance): add access request details and governance service\n\nCo-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>\n' > /tmp/msg
git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg
```

---

### Task 3: Data Owner exemption in the guardrail evaluator

**Files:**
- Modify: `src/gateway/Nlp/Guardrails/GuardrailResult.cs`
- Modify: `src/gateway/Nlp/Guardrails/GuardrailEvaluator.cs`
- Test: `tests/Gateway.Tests/Nlp/GuardrailEvaluatorExemptionTests.cs`

**Interfaces:**
- Consumes: `RoleNormalizer` (Task 1), `AccessRequest.ExemptionOwnerAccess` (Task 2).
- Produces: `GuardrailEvaluator.Evaluate(string? pipelineJson, string? requesterRole = null)`; `GuardrailResult` trailing `string? ExemptionType = null`.

- [ ] **Step 1: Write the failing test**

Create `tests/Gateway.Tests/Nlp/GuardrailEvaluatorExemptionTests.cs`:

```csharp
using Gateway.Nlp.Guardrails;

namespace Gateway.Tests.Nlp;

public class GuardrailEvaluatorExemptionTests
{
    private const string CoordinatesPipeline =
        """[{"$match":{"address.location.coordinates.lat":{"$gte":40}}}]""";

    [Fact]
    public void Data_owner_is_exempt_from_the_approval_pause()
    {
        var evaluator = GuardrailTestFactory.FromAssets();

        var result = evaluator.Evaluate(CoordinatesPipeline, "Data Owner / Admin");

        Assert.Equal(GuardrailOutcome.Allowed, result.Outcome);
        Assert.Equal(AccessRequest.ExemptionOwnerAccess, result.ExemptionType);
        Assert.Contains("address.location.coordinates", result.SensitiveFields);
    }

    [Fact]
    public void Business_analyst_is_not_exempt()
    {
        var evaluator = GuardrailTestFactory.FromAssets();

        var result = evaluator.Evaluate(CoordinatesPipeline, "Business Analyst");

        Assert.Equal(GuardrailOutcome.PausedForApproval, result.Outcome);
        Assert.Null(result.ExemptionType);
    }

    [Fact]
    public void Unknown_role_is_not_exempt()
    {
        var evaluator = GuardrailTestFactory.FromAssets();

        var result = evaluator.Evaluate(CoordinatesPipeline, null);

        Assert.Equal(GuardrailOutcome.PausedForApproval, result.Outcome);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~GuardrailEvaluatorExemptionTests"
```

Expected: FAIL to compile because the two-argument `Evaluate` overload and `ExemptionType` do not exist.

- [ ] **Step 3: Add the exemption field to the result**

Replace `src/gateway/Nlp/Guardrails/GuardrailResult.cs` with:

```csharp
namespace Gateway.Nlp.Guardrails;

public sealed record GuardrailResult(
    GuardrailOutcome Outcome,
    string? Reason,
    IReadOnlyList<string> SensitiveFields,
    IReadOnlyList<string> UnknownFields,
    IReadOnlyList<string> BlockedOperators,
    string? ExemptionType = null)
{
    public static readonly GuardrailResult Allowed = new(GuardrailOutcome.Allowed, null, [], [], []);
}
```

- [ ] **Step 4: Apply the exemption in the evaluator**

Replace the approval branch in `src/gateway/Nlp/Guardrails/GuardrailEvaluator.cs` (the `if (approvalFields.Count > 0 && _flags.Enabled)` block) and the method signature with:

```csharp
    public GuardrailResult Evaluate(string? pipelineJson, string? requesterRole = null)
```

and the block:

```csharp
        if (approvalFields.Count > 0 && _flags.Enabled)
        {
            var approvalFlagList = matched.Where(flag => flag.RequiresApproval).ToList();
            if (IsExempt(requesterRole, approvalFlagList))
            {
                return new GuardrailResult(
                    GuardrailOutcome.Allowed,
                    null,
                    sensitiveFields,
                    [],
                    [],
                    AccessRequest.ExemptionOwnerAccess);
            }

            return new GuardrailResult(
                GuardrailOutcome.PausedForApproval,
                $"sensitive field requires approval: {string.Join(", ", approvalFields)}",
                approvalFields,
                [],
                []);
        }
```

Add this private static method before the closing brace:

```csharp
    private static bool IsExempt(string? requesterRole, IReadOnlyList<SensitiveFieldFlag> approvalFlags)
    {
        if (string.IsNullOrWhiteSpace(requesterRole) || approvalFlags.Count == 0)
        {
            return false;
        }

        return approvalFlags.All(flag =>
            flag.DataOwnerRoles.Any(ownerRole => RoleNormalizer.Matches(requesterRole, ownerRole)));
    }
```

Add `using Gateway.Governance;` to the top of `GuardrailEvaluator.cs` if it is not already present (it currently imports `Gateway.Governance` for `IApprovalFlagStore`, so no change is needed).

- [ ] **Step 5: Run the tests to verify they pass**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~GuardrailEvaluator"
```

Expected: PASS, including the existing `GuardrailEvaluatorFlagTests` and `GuardrailEvaluatorTests`.

- [ ] **Step 6: Commit**

```bash
cd /workspace
git add src/gateway/Nlp/Guardrails/GuardrailResult.cs src/gateway/Nlp/Guardrails/GuardrailEvaluator.cs tests/Gateway.Tests/Nlp/GuardrailEvaluatorExemptionTests.cs
printf 'feat(guardrails): exempt data owners from the approval pause\n\nCo-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>\n' > /tmp/msg
git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg
```

---

### Task 4: Requester context and exemption in the NLP orchestrator

**Files:**
- Modify: `src/gateway/Nlp/Orchestrator/INlpOrchestrator.cs`
- Modify: `src/gateway/Nlp/Orchestrator/NlpOrchestrator.cs`
- Modify: `src/gateway/Nlp/Orchestrator/AgentState.cs`
- Modify: `tests/Gateway.Tests/Conversations/ConversationOrchestratorApprovalTests.cs` (stub signature)
- Modify: `tests/Gateway.Tests/Conversations/ConversationDiTests.cs` (stub signature)
- Test: `tests/Gateway.Tests/Nlp/NlpOrchestratorExemptionTests.cs`
- Test: `tests/Gateway.Tests/Nlp/NlpOrchestratorGuardrailTests.cs` (assert enrichment)

**Interfaces:**
- Consumes: `RequesterContext` (Task 1), `RequesterInfo`/`RequestedFlag` (Task 2), `GuardrailResult.ExemptionType` (Task 3).
- Produces: `INlpOrchestrator.OrchestrateAsync(string utterance, string sessionId = "anonymous", CancellationToken cancellationToken = default, RequesterContext? requester = null)`; paused `AccessRequest` carries `Requester`, `AssignedLeadId`, `RequestedFlags`; `AgentState` trailing `string? AccessRequestId = null`.

- [ ] **Step 1: Write the failing test**

Create `tests/Gateway.Tests/Nlp/NlpOrchestratorExemptionTests.cs`:

```csharp
using Gateway.Governance;
using Gateway.Nlp.Abstractions;
using Gateway.Nlp.Cache;
using Gateway.Nlp.Guardrails;
using Gateway.Nlp.Llm;
using Gateway.Nlp.Mql;
using Gateway.Nlp.Orchestrator;
using Gateway.Nlp.Router;
using Gateway.Nlp.Slots;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Nlp;

public class NlpOrchestratorExemptionTests
{
    private sealed class StubGenerator : ILlmQueryGenerator
    {
        private readonly string _pipeline;

        public StubGenerator(string pipeline) => _pipeline = pipeline;

        public Task<LlmQueryResult> GenerateAsync(string utterance, string slotsJson, string? previousError, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LlmQueryResult(_pipeline, 7));
        }
    }

    private sealed class VectorEmbedder : ITextEmbedder
    {
        public float[] Embed(string text) => new float[] { 1, 0, 0, 0, 0, 0, 0, 0 };
    }

    private static (NlpOrchestrator Sut, InMemoryAccessRequestStore Requests) Build()
    {
        var gazetteer = Gazetteer.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Gazetteers"));
        var builder = ScribanSimpleMqlBuilder.FromAssetsDirectory(Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Templates"));
        var router = new NlpRouter(new VectorEmbedder(), new SemanticCache(), builder, gazetteer);
        var corrector = new SelfCorrectingLlmQueryGenerator(
            new StubGenerator("""[{"$match":{"address.location.coordinates.lat":{"$gte":40}}}]"""),
            new PipelineValidator(),
            Options.Create(new NvidiaNimOptions { MaxAttempts = 3 }));
        var requests = new InMemoryAccessRequestStore();
        var sut = new NlpOrchestrator(
            router,
            gazetteer,
            corrector,
            new InMemoryAgentEventSink(),
            new InMemoryAgentStateStore(),
            GuardrailTestFactory.FromAssets(),
            requests,
            TimeProvider.System);
        return (sut, requests);
    }

    [Fact]
    public async Task Data_owner_does_not_create_a_pending_request()
    {
        var (sut, requests) = Build();

        var result = await sut.OrchestrateAsync(
            "average coordinates near me",
            "sess_1",
            default,
            new RequesterContext("usr_owner", "Owner", "Data Owner / Admin", null));

        Assert.NotEqual(NlpRouteKind.GovernancePaused, result.Kind);
        Assert.Empty(await requests.ListAsync());
    }

    [Fact]
    public async Task Analyst_pending_request_carries_requester_and_lead()
    {
        var (sut, requests) = Build();

        var result = await sut.OrchestrateAsync(
            "average coordinates near me",
            "sess_1",
            default,
            new RequesterContext("usr_analyst", "Analyst", "Business Analyst", "usr_lead"));

        Assert.Equal(NlpRouteKind.GovernancePaused, result.Kind);
        var stored = await requests.GetAsync(result.AccessRequestId!);
        Assert.NotNull(stored);
        Assert.Equal("usr_analyst", stored!.Requester!.UserId);
        Assert.Equal("usr_lead", stored.AssignedLeadId);
        Assert.Contains(stored.RequestedFlags!, flag => flag.FieldPath == "address.location.coordinates");
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~NlpOrchestratorExemptionTests"
```

Expected: FAIL to compile because the `requester` parameter is missing.

- [ ] **Step 3: Extend the interface and state**

Replace `src/gateway/Nlp/Orchestrator/INlpOrchestrator.cs` with:

```csharp
using Gateway.Governance;
using Gateway.Nlp.Router;

namespace Gateway.Nlp.Orchestrator;

public interface INlpOrchestrator
{
    Task<NlpRouteResult> OrchestrateAsync(
        string utterance,
        string sessionId = "anonymous",
        CancellationToken cancellationToken = default,
        RequesterContext? requester = null);
}
```

Replace `src/gateway/Nlp/Orchestrator/AgentState.cs` with:

```csharp
namespace Gateway.Nlp.Orchestrator;

public sealed record AgentState(
    string SessionId,
    string Stage,
    string? PendingQuestion,
    DateTimeOffset UpdatedAt,
    string? AccessRequestId = null);
```

- [ ] **Step 4: Update the orchestrator**

In `src/gateway/Nlp/Orchestrator/NlpOrchestrator.cs`, add `using Gateway.Governance;` if missing, change the method signature to:

```csharp
    public async Task<NlpRouteResult> OrchestrateAsync(
        string utterance,
        string sessionId = "anonymous",
        CancellationToken cancellationToken = default,
        RequesterContext? requester = null)
```

Replace the paused block (from `if (guard.Outcome == GuardrailOutcome.PausedForApproval)` through its closing brace) with:

```csharp
        if (guard.ExemptionType is not null)
        {
            _events.Publish(new AgentEvent("governance.exempted", "exempted", guard.ExemptionType));
            return result with { SensitiveFields = guard.SensitiveFields };
        }

        if (guard.Outcome == GuardrailOutcome.PausedForApproval)
        {
            var request = await _accessRequests.CreateAsync(
                new AccessRequest(
                    string.Empty,
                    sessionId,
                    result.Mql,
                    guard.SensitiveFields,
                    AccessRequest.PendingLead,
                    Justification: null,
                    _clock.GetUtcNow(),
                    Requester: requester is null
                        ? null
                        : new RequesterInfo(requester.UserId, requester.Name, requester.Role, requester.LeadUserId),
                    AssignedLeadId: requester?.LeadUserId,
                    RequestedFlags: guard.SensitiveFields
                        .Select(field => new RequestedFlag(field, "requires_approval"))
                        .ToList()),
                cancellationToken);

            _events.Publish(new AgentEvent("governance.paused", "paused", guard.Reason));
            await _stateStore.SaveAsync(
                new AgentState(sessionId, "governance_paused", guard.Reason, _clock.GetUtcNow(), request.Id),
                cancellationToken);

            return result with
            {
                Kind = NlpRouteKind.GovernancePaused,
                SensitiveFields = guard.SensitiveFields,
                AccessRequestId = request.Id,
                GuardrailReason = guard.Reason
            };
        }
```

- [ ] **Step 5: Update the interface implementations**

In `tests/Gateway.Tests/Conversations/ConversationOrchestratorApprovalTests.cs`, change `StubOrchestrator.OrchestrateAsync` to:

```csharp
        public Task<NlpRouteResult> OrchestrateAsync(
            string utterance,
            string sessionId = "anonymous",
            CancellationToken cancellationToken = default,
            Gateway.Governance.RequesterContext? requester = null)
            => Task.FromResult(_result);
```

In `tests/Gateway.Tests/Conversations/ConversationDiTests.cs`, change `StubNlpOrchestrator.OrchestrateAsync` to:

```csharp
        public Task<NlpRouteResult> OrchestrateAsync(
            string utterance,
            string sessionId = "anonymous",
            CancellationToken cancellationToken = default,
            Gateway.Governance.RequesterContext? requester = null) =>
            throw new NotImplementedException();
```

If any other type implements `INlpOrchestrator`, update it the same way. Find them with:

```bash
rg -n ": INlpOrchestrator" src tests
```

- [ ] **Step 6: Run the affected tests**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~NlpOrchestrator"
```

Expected: PASS, including the existing `NlpOrchestratorGuardrailTests`.

- [ ] **Step 7: Commit**

```bash
cd /workspace
git add src/gateway/Nlp/Orchestrator tests/Gateway.Tests/Nlp/NlpOrchestratorExemptionTests.cs tests/Gateway.Tests/Conversations/ConversationOrchestratorApprovalTests.cs tests/Gateway.Tests/Conversations/ConversationDiTests.cs
printf 'feat(governance): thread requester context and exemption through the orchestrator\n\nCo-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>\n' > /tmp/msg
git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg
```

---

### Task 5: Resume plumbing

**Files:**
- Modify: `src/gateway/Conversations/IConversationStore.cs`
- Modify: `src/gateway/Conversations/InMemoryConversationStore.cs`
- Modify: `src/gateway/Conversations/ConversationOrchestrator.cs`
- Test: `tests/Gateway.Tests/Conversations/ConversationResumeTests.cs`

**Interfaces:**
- Consumes: `IConversationResumeHandler` (Task 2).
- Produces: `IConversationStore.FindByAccessRequestAsync(string, CancellationToken)`; `ConversationOrchestrator : IConversationResumeHandler` with `ResumeAsync`.

- [ ] **Step 1: Write the failing test**

Create `tests/Gateway.Tests/Conversations/ConversationResumeTests.cs`:

```csharp
using Gateway.Conversations;
using Gateway.Governance;
using Gateway.Nlp.Http;
using Gateway.Nlp.Orchestrator;
using Gateway.Nlp.Router;
using Gateway.Reports;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Conversations;

public class ConversationResumeTests
{
    private sealed class StubOrchestrator : INlpOrchestrator
    {
        public Task<NlpRouteResult> OrchestrateAsync(
            string utterance,
            string sessionId = "anonymous",
            CancellationToken cancellationToken = default,
            RequesterContext? requester = null) =>
            throw new NotImplementedException();
    }

    private static (ConversationOrchestrator Sut, IConversationStore Store) Build()
    {
        var store = new InMemoryConversationStore();
        var sut = new ConversationOrchestrator(
            new StubOrchestrator(),
            store,
            new InMemoryAccessRequestStore(),
            new InMemoryApprovalFlagStore(true),
            new SimulatedNotificationSender(),
            new ColumnCatalog(),
            new InMemoryAgentEventSink(),
            Options.Create(new ReportsOptions()),
            TimeProvider.System);
        return (sut, store);
    }

    private static ConversationState Paused(string id, string requestId, string delivery) => new(
        id,
        "sess_1",
        ConversationStep.Complete,
        "average coordinates near me",
        "[]",
        NlpRouteKind.GovernancePaused,
        requestId,
        ["address.location.coordinates"],
        false,
        true,
        new ReportIntakeDraft(
            RequesterEmail: "analyst@enterprise.com",
            Purpose: "Geo analysis",
            BusinessImpact: "Target market planning",
            ProjectCode: "PROJ-1",
            ManagerEmail: "manager@enterprise.com",
            Columns: ["name"],
            DeliveryFormat: delivery),
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task Resume_lifts_the_hold_and_unlocks_csv()
    {
        var (sut, store) = Build();
        await store.CreateAsync(Paused("conv_1", "req_1", ReportIntake.Csv));

        var resumed = await sut.ResumeAsync("req_1");

        Assert.True(resumed);
        var turn = await sut.GetAsync("conv_1");
        Assert.False(turn!.ApprovalRequired);
        Assert.True(turn.Downloadable);
    }

    [Fact]
    public async Task Resume_without_a_conversation_is_a_no_op()
    {
        var (sut, _) = Build();

        var resumed = await sut.ResumeAsync("missing");

        Assert.False(resumed);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ConversationResumeTests"
```

Expected: FAIL to compile because `FindByAccessRequestAsync` and `ResumeAsync` do not exist.

- [ ] **Step 3: Add the store lookup**

Replace `src/gateway/Conversations/IConversationStore.cs` with:

```csharp
namespace Gateway.Conversations;

public interface IConversationStore
{
    Task<ConversationState> CreateAsync(ConversationState state, CancellationToken cancellationToken = default);

    Task<ConversationState?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task<ConversationState?> FindByAccessRequestAsync(string accessRequestId, CancellationToken cancellationToken = default);

    Task<ConversationState> UpdateAsync(ConversationState state, CancellationToken cancellationToken = default);
}
```

Add to `src/gateway/Conversations/InMemoryConversationStore.cs` after `GetAsync`:

```csharp
    public Task<ConversationState?> FindByAccessRequestAsync(string accessRequestId, CancellationToken cancellationToken = default)
    {
        var match = _states.Values.FirstOrDefault(state => state.AccessRequestId == accessRequestId);
        return Task.FromResult(match);
    }
```

- [ ] **Step 4: Implement the resume handler**

In `src/gateway/Conversations/ConversationOrchestrator.cs`:

Change the class declaration to:

```csharp
public sealed partial class ConversationOrchestrator : IConversationResumeHandler
```

Add this method after `GetAsync`:

```csharp
    public async Task<bool> ResumeAsync(string accessRequestId, CancellationToken cancellationToken = default)
    {
        var state = await _store.FindByAccessRequestAsync(accessRequestId, cancellationToken);
        if (state is null || !state.ApprovalRequired)
        {
            return false;
        }

        var resumed = await _store.UpdateAsync(
            state with { ApprovalRequired = false, UpdatedAt = _clock.GetUtcNow() },
            cancellationToken);

        _events.Publish(new AgentEvent("conversation.resumed", "resumed", resumed.Id));
        if (resumed.Draft.DeliveryFormat == ReportIntake.Csv)
        {
            _events.Publish(new AgentEvent("report.ready", "ready", resumed.Id));
        }

        return true;
    }
```

- [ ] **Step 5: Run the tests to verify they pass**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~Conversation"
```

Expected: PASS, including the existing conversation tests.

- [ ] **Step 6: Commit**

```bash
cd /workspace
git add src/gateway/Conversations/IConversationStore.cs src/gateway/Conversations/InMemoryConversationStore.cs src/gateway/Conversations/ConversationOrchestrator.cs tests/Gateway.Tests/Conversations/ConversationResumeTests.cs
printf 'feat(conversations): resume paused conversations after approval\n\nCo-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>\n' > /tmp/msg
git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg
```

---

### Task 6: Business-impact intake step

**Files:**
- Modify: `src/gateway/Conversations/ConversationStep.cs`
- Modify: `src/gateway/Conversations/ConversationControl.cs`
- Modify: `src/gateway/Conversations/ConversationAnswer.cs`
- Modify: `src/gateway/Conversations/ReportIntakeDraft.cs`
- Modify: `src/gateway/Conversations/ConversationOrchestrator.cs`
- Modify: `tests/Gateway.Tests/Conversations/ConversationOrchestratorApprovalTests.cs`
- Test: `tests/Gateway.Tests/Conversations/ConversationBusinessImpactTests.cs`

**Interfaces:**
- Consumes: `GovernanceJustification` (Task 2).
- Produces: `ConversationStep.BusinessImpact`, `ConversationControl.BusinessImpact`, `ConversationAnswer.BusinessImpact`, `ReportIntakeDraft.BusinessImpact`; gated intake requires project code and stores `JustificationDetails`.

- [ ] **Step 1: Write the failing test**

Create `tests/Gateway.Tests/Conversations/ConversationBusinessImpactTests.cs`:

```csharp
using Gateway.Conversations;
using Gateway.Nlp.Intent;
using Gateway.Nlp.Mql;
using Gateway.Nlp.Orchestrator;
using Gateway.Nlp.Router;
using Gateway.Reports;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Conversations;

public class ConversationBusinessImpactTests
{
    private sealed class StubOrchestrator : INlpOrchestrator
    {
        private readonly NlpRouteResult _result;

        public StubOrchestrator(NlpRouteResult result) => _result = result;

        public Task<NlpRouteResult> OrchestrateAsync(
            string utterance,
            string sessionId = "anonymous",
            CancellationToken cancellationToken = default,
            Gateway.Governance.RequesterContext? requester = null)
            => Task.FromResult(_result);
    }

    private static NlpRouteResult Paused() => new(
        NlpRouteKind.GovernancePaused,
        """[{"$match":{"address.location.coordinates.lat":{"$gte":40}}}]""",
        null,
        false,
        false,
        IntentKind.Search,
        false,
        new MqlDefaults(10, "rating_desc", "All"),
        0,
        1,
        null,
        ["address.location.coordinates"],
        "req_1",
        "sensitive field requires approval: address.location.coordinates");

    private static async Task<(ConversationOrchestrator Sut, ConversationTurn Turn)> StartAsync()
    {
        var sut = new ConversationOrchestrator(
            new StubOrchestrator(Paused()),
            new InMemoryConversationStore(),
            new InMemoryAccessRequestStore(),
            new InMemoryApprovalFlagStore(true),
            new SimulatedNotificationSender(),
            new ColumnCatalog(),
            new InMemoryAgentEventSink(),
            Options.Create(new ReportsOptions()),
            TimeProvider.System);
        var turn = await sut.StartAsync("average coordinates near me", "sess_1", "analyst@enterprise.com");
        turn = await sut.AnswerAsync(turn.ConversationId, new ConversationAnswer(Email: "analyst@enterprise.com"));
        return (sut, turn);
    }

    [Fact]
    public async Task Gated_intake_requires_a_business_impact_step()
    {
        var (sut, turn) = await StartAsync();

        turn = await sut.AnswerAsync(turn.ConversationId, new ConversationAnswer(Purpose: "Geo analysis", ProjectCode: "PROJ-1"));

        Assert.Equal("BusinessImpact", turn!.Step);
        Assert.Equal("businessimpact", turn.Control);
    }

    [Fact]
    public async Task Gated_purpose_requires_a_project_code()
    {
        var (sut, turn) = await StartAsync();

        turn = await sut.AnswerAsync(turn.ConversationId, new ConversationAnswer(Purpose: "Geo analysis"));

        Assert.Equal("Purpose", turn!.Step);
        Assert.Equal("A project code is required for sensitive requests.", turn.ValidationError);
    }

    [Fact]
    public async Task Business_impact_is_validated_and_stored()
    {
        var (sut, turn) = await StartAsync();
        turn = await sut.AnswerAsync(turn.ConversationId, new ConversationAnswer(Purpose: "Geo analysis", ProjectCode: "PROJ-1"));

        var invalid = await sut.AnswerAsync(turn.ConversationId, new ConversationAnswer(BusinessImpact: "short"));
        Assert.Equal("BusinessImpact", invalid!.Step);
        Assert.NotNull(invalid.ValidationError);

        var ok = await sut.AnswerAsync(turn.ConversationId, new ConversationAnswer(BusinessImpact: "Target market expansion planning"));
        Assert.Equal("ManagerEmail", ok!.Step);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ConversationBusinessImpactTests"
```

Expected: FAIL to compile because the `BusinessImpact` members do not exist.

- [ ] **Step 3: Add the enum and record members**

Replace `src/gateway/Conversations/ConversationStep.cs` with:

```csharp
namespace Gateway.Conversations;

public enum ConversationStep
{
    Email,
    Purpose,
    BusinessImpact,
    ManagerEmail,
    Columns,
    Delivery,
    Complete
}
```

Replace `src/gateway/Conversations/ConversationControl.cs` with:

```csharp
namespace Gateway.Conversations;

public enum ConversationControl
{
    None,
    Email,
    Purpose,
    BusinessImpact,
    Columns,
    Delivery
}
```

Replace `src/gateway/Conversations/ConversationAnswer.cs` with:

```csharp
namespace Gateway.Conversations;

public sealed record ConversationAnswer(
    string? Text = null,
    string? Email = null,
    string? Purpose = null,
    string? ProjectCode = null,
    string? BusinessImpact = null,
    string? ManagerEmail = null,
    IReadOnlyList<string>? Columns = null,
    string? Delivery = null);
```

Replace `src/gateway/Conversations/ReportIntakeDraft.cs` with:

```csharp
namespace Gateway.Conversations;

public sealed record ReportIntakeDraft(
    string? RequesterEmail = null,
    string? Purpose = null,
    string? ProjectCode = null,
    string? BusinessImpact = null,
    string? ManagerEmail = null,
    IReadOnlyList<string>? Columns = null,
    string? DeliveryFormat = null);
```

- [ ] **Step 4: Update the step machine**

In `src/gateway/Conversations/ConversationOrchestrator.cs`, replace the `case ConversationStep.Purpose:` block with:

```csharp
            case ConversationStep.Purpose:
            {
                var purpose = FirstNonEmpty(answer.Purpose, answer.Text);
                if (string.IsNullOrWhiteSpace(purpose) || purpose.Trim().Length < 5 || purpose.Trim().Length > 500)
                {
                    return ToTurn(state) with { ValidationError = "Describe the purpose in 5 to 500 characters." };
                }

                var projectCode = FirstNonEmpty(answer.ProjectCode, null);
                if (projectCode is not null && !ProjectCodePattern().IsMatch(projectCode))
                {
                    return ToTurn(state) with { ValidationError = "The project code may contain letters, digits, and hyphens only." };
                }

                var gated = state.Kind == NlpRouteKind.GovernancePaused;
                if (gated && string.IsNullOrWhiteSpace(projectCode))
                {
                    return ToTurn(state) with { ValidationError = "A project code is required for sensitive requests." };
                }

                draft = draft with { Purpose = purpose.Trim(), ProjectCode = projectCode?.Trim() };
                state = state with
                {
                    Step = gated ? ConversationStep.BusinessImpact : ConversationStep.ManagerEmail
                };
                break;
            }

            case ConversationStep.BusinessImpact:
            {
                var impact = FirstNonEmpty(answer.BusinessImpact, answer.Text);
                if (string.IsNullOrWhiteSpace(impact) || impact.Trim().Length < 8 || impact.Trim().Length > 500)
                {
                    return ToTurn(state) with { ValidationError = "Describe the business impact in 8 to 500 characters." };
                }

                draft = draft with { BusinessImpact = impact.Trim() };
                state = state with { Step = ConversationStep.ManagerEmail };
                break;
            }
```

In `ToTurn`, add to the control switch:

```csharp
            ConversationStep.BusinessImpact => ConversationControl.BusinessImpact,
```

In `StepMessage`, add:

```csharp
            ConversationStep.BusinessImpact => "What is the business impact of this request?",
```

- [ ] **Step 5: Store the justification on finalize**

In `FinalizeAsync`, replace the `if (approvalRequired && state.AccessRequestId is not null)` block header with:

```csharp
        var justification = approvalRequired
            ? new GovernanceJustification(draft.Purpose!, draft.BusinessImpact!, draft.ProjectCode)
            : null;

        if (approvalRequired && state.AccessRequestId is not null)
        {
            var existing = await _accessRequests.GetAsync(state.AccessRequestId, cancellationToken);
            if (existing is not null)
            {
                await _accessRequests.UpdateAsync(
                    existing with { Intake = intake, Justification = intake.Purpose, JustificationDetails = justification },
                    cancellationToken);
            }
```

Keep the rest of the block unchanged.

- [ ] **Step 6: Update the existing approval test**

In `tests/Gateway.Tests/Conversations/ConversationOrchestratorApprovalTests.cs`, in `RunAsync`, insert the business-impact answer between the purpose and manager answers:

```csharp
        turn = await sut.AnswerAsync(turn.ConversationId, new ConversationAnswer(Purpose: "Geo analysis", ProjectCode: "PROJ-1"));
        turn = await sut.AnswerAsync(turn.ConversationId, new ConversationAnswer(BusinessImpact: "Target market expansion planning"));
        turn = await sut.AnswerAsync(turn.ConversationId, new ConversationAnswer(ManagerEmail: "manager@enterprise.com"));
```

Remove the old standalone purpose line. Then in `Sensitive_completion_requires_approval_and_notifies_the_manager`, add:

```csharp
        Assert.Equal("Target market expansion planning", stored!.JustificationDetails!.BusinessImpact);
```

after the existing `stored` assertion.

- [ ] **Step 7: Run the conversation tests**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~Conversation"
```

Expected: PASS, including the existing conversation tests.

- [ ] **Step 8: Commit**

```bash
cd /workspace
git add src/gateway/Conversations src/gateway/Nlp/Guardrails/GovernanceJustification.cs tests/Gateway.Tests/Conversations
printf 'feat(conversations): capture business impact for gated requests\n\nCo-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>\n' > /tmp/msg
git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg
```

---

### Task 7: Thread requester context through the conversation start

**Files:**
- Modify: `src/gateway/Conversations/ConversationOrchestrator.cs`
- Modify: `src/gateway/Program.cs`
- Modify: `tests/Gateway.Tests/ApiConversationTests.cs` (if it asserts payload shape; otherwise no change)
- Test: `tests/Gateway.Tests/Conversations/ConversationRequesterTests.cs`

**Interfaces:**
- Consumes: `RequesterContext` (Task 1).
- Produces: `ConversationOrchestrator.StartAsync(string utterance, string sessionId, string? requesterEmail, CancellationToken cancellationToken = default, RequesterContext? requester = null)`.

- [ ] **Step 1: Write the failing test**

Create `tests/Gateway.Tests/Conversations/ConversationRequesterTests.cs`:

```csharp
using Gateway.Conversations;
using Gateway.Governance;
using Gateway.Nlp.Guardrails;
using Gateway.Nlp.Intent;
using Gateway.Nlp.Mql;
using Gateway.Nlp.Orchestrator;
using Gateway.Nlp.Router;
using Gateway.Nlp.Http;
using Gateway.Reports;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Conversations;

public class ConversationRequesterTests
{
    private sealed class StubOrchestrator : INlpOrchestrator
    {
        public RequesterContext? LastRequester { get; private set; }

        public Task<NlpRouteResult> OrchestrateAsync(
            string utterance,
            string sessionId = "anonymous",
            CancellationToken cancellationToken = default,
            RequesterContext? requester = null)
        {
            LastRequester = requester;
            var result = new NlpRouteResult(
                NlpRouteKind.GovernancePaused,
                "[]",
                null,
                false,
                false,
                IntentKind.Search,
                false,
                new MqlDefaults(10, "rating_desc", "All"),
                0,
                1,
                null,
                ["address.location.coordinates"],
                "req_1",
                "sensitive field requires approval: address.location.coordinates");
            return Task.FromResult(result);
        }
    }

    [Fact]
    public async Task Start_passes_the_requester_context_to_the_orchestrator()
    {
        var stub = new StubOrchestrator();
        var sut = new ConversationOrchestrator(
            stub,
            new InMemoryConversationStore(),
            new InMemoryAccessRequestStore(),
            new InMemoryApprovalFlagStore(true),
            new SimulatedNotificationSender(),
            new ColumnCatalog(),
            new InMemoryAgentEventSink(),
            Options.Create(new ReportsOptions()),
            TimeProvider.System);

        await sut.StartAsync(
            "average coordinates near me",
            "sess_1",
            "analyst@enterprise.com",
            default,
            new RequesterContext("usr_analyst", "Analyst", "Business Analyst", "usr_lead"));

        Assert.NotNull(stub.LastRequester);
        Assert.Equal("usr_analyst", stub.LastRequester!.UserId);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ConversationRequesterTests"
```

Expected: FAIL to compile because `StartAsync` has no `requester` parameter.

- [ ] **Step 3: Add the parameter and pass it through**

In `src/gateway/Conversations/ConversationOrchestrator.cs`, add `using Gateway.Governance;` if missing (it already imports it). Replace the `StartAsync` signature with:

```csharp
    public async Task<ConversationTurn> StartAsync(
        string utterance,
        string sessionId,
        string? requesterEmail,
        CancellationToken cancellationToken = default,
        RequesterContext? requester = null)
```

and change the orchestrator call to:

```csharp
        var result = await _nlp.OrchestrateAsync(utterance, sessionId, cancellationToken, requester);
```

- [ ] **Step 4: Pass the context from the endpoints**

In `src/gateway/Program.cs`, change the `/api/nlp/query` call:

```csharp
    var result = await orchestrator.OrchestrateAsync(utterance, sessionId, cancellationToken, SessionClaims.ToRequesterContext(user));
```

and the `/api/conversations` call:

```csharp
    var turn = await conversations.StartAsync(utterance, sessionId, email, cancellationToken, SessionClaims.ToRequesterContext(user));
```

- [ ] **Step 5: Run the conversation and API tests**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~Conversation|FullyQualifiedName~ApiConversation"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
cd /workspace
git add src/gateway/Conversations/ConversationOrchestrator.cs src/gateway/Program.cs tests/Gateway.Tests/Conversations/ConversationRequesterTests.cs
printf 'feat(conversations): pass requester context from the endpoints\n\nCo-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>\n' > /tmp/msg
git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg
```

---

### Task 8: Wire governance services into DI

**Files:**
- Modify: `src/gateway/Nlp/NlpServiceCollectionExtensions.cs`
- Modify: `tests/Gateway.Tests/Conversations/ConversationDiTests.cs`

**Interfaces:**
- Consumes: `GovernanceService` (Task 2), `ConversationOrchestrator : IConversationResumeHandler` (Task 5).
- Produces: `IGovernanceService` and `IConversationResumeHandler` resolvable from DI, with the resume handler pointing at the singleton `ConversationOrchestrator`.

- [ ] **Step 1: Write the failing test**

In `tests/Gateway.Tests/Conversations/ConversationDiTests.cs`, add `using Gateway.Governance;` and extend the existing test assertions:

```csharp
        Assert.NotNull(provider.GetService<IGovernanceService>());
        Assert.NotNull(provider.GetService<IConversationResumeHandler>());
        Assert.Same(provider.GetService<ConversationOrchestrator>(), provider.GetService<IConversationResumeHandler>());
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ConversationDiTests"
```

Expected: FAIL because `IGovernanceService` is not registered.

- [ ] **Step 3: Register the services**

In `src/gateway/Nlp/NlpServiceCollectionExtensions.cs`, replace:

```csharp
        services.AddSingleton<ConversationOrchestrator>();
```

with:

```csharp
        services.AddSingleton<ConversationOrchestrator>();
        services.AddSingleton<IConversationResumeHandler>(sp => sp.GetRequiredService<ConversationOrchestrator>());
        services.AddSingleton<IGovernanceService, GovernanceService>();
```

`using Gateway.Governance;` is already present at the top of the file.

- [ ] **Step 4: Run the test to verify it passes**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ConversationDiTests"
```

Expected: PASS. If a circular dependency error appears, confirm `NlpOrchestrator` and `ConversationOrchestrator` depend on `IAccessRequestStore` (not `IGovernanceService`); only `GovernanceService` may depend on `IConversationResumeHandler`.

- [ ] **Step 5: Commit**

```bash
cd /workspace
git add src/gateway/Nlp/NlpServiceCollectionExtensions.cs tests/Gateway.Tests/Conversations/ConversationDiTests.cs
printf 'feat(governance): register governance and resume services\n\nCo-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>\n' > /tmp/msg
git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg
```

---

### Task 9: Access-request API endpoints

**Files:**
- Modify: `src/gateway/Program.cs`
- Test: `tests/Gateway.Tests/ApiGovernanceTests.cs`

**Interfaces:**
- Consumes: `IGovernanceService`, `Governance*Exception`, `SessionClaims.ToRequesterContext`, `AccessRequest` (Tasks 1, 2, 8), `ConversationOrchestrator`/`IConversationStore` for resume.
- Produces: `GET /api/access-requests`, `GET /api/access-requests/{id}`, `POST /api/access-requests/{id}/approve`, `POST /api/access-requests/{id}/reject`, `POST /api/access-requests/{id}/override`.

- [ ] **Step 1: Write the failing test**

Create `tests/Gateway.Tests/ApiGovernanceTests.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Gateway.Conversations;
using Gateway.Nlp.Guardrails;
using Gateway.Nlp.Router;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Gateway.Tests;

public class ApiGovernanceTests : IClassFixture<GatewayFactory>
{
    private readonly GatewayFactory _factory;

    public ApiGovernanceTests(GatewayFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Queue_requires_a_token()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/access-requests");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Analyst_queue_contains_only_own_requests()
    {
        var request = await SeedPendingAsync("usr_analyst", "usr_lead");
        var client = Client("usr_analyst", "Business Analyst", "usr_lead");

        var results = await client.GetFromJsonAsync<List<AccessRequestDto>>("/api/access-requests");

        Assert.NotNull(results);
        Assert.Contains(results!, item => item.Id == request.Id);
        Assert.All(results!, item => Assert.Equal("usr_analyst", item.Requester!.UserId));
    }

    [Fact]
    public async Task Team_lead_approves_an_assigned_request_and_resumes_the_report()
    {
        var request = await SeedPendingAsync("usr_analyst", "usr_lead");
        var conversation = await SeedPausedConversationAsync(request.Id);
        var client = Client("usr_lead", "Team Lead", "usr_director");

        var response = await client.PostAsJsonAsync($"/api/access-requests/{request.Id}/approve", new { notes = "approved" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<AccessRequestDto>();
        Assert.Equal("APPROVED", updated!.Status);
        Assert.False(updated.Resolution!.OverrideInvoked);

        var turn = await client.GetFromJsonAsync<ConversationTurnDto>($"/api/conversations/{conversation}");
        Assert.False(turn!.ApprovalRequired);
        Assert.True(turn.Downloadable);
    }

    [Fact]
    public async Task Analyst_cannot_approve()
    {
        var request = await SeedPendingAsync("usr_analyst", "usr_lead");
        var client = Client("usr_analyst", "Business Analyst", "usr_lead");

        var response = await client.PostAsJsonAsync($"/api/access-requests/{request.Id}/approve", new { notes = "x" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Manager_override_records_tags_and_requires_notes()
    {
        var request = await SeedPendingAsync("usr_analyst", "usr_lead");
        var client = Client("usr_mgr", "Engineering Manager", null);

        var blank = await client.PostAsJsonAsync($"/api/access-requests/{request.Id}/override", new { notes = "" });
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);

        var response = await client.PostAsJsonAsync($"/api/access-requests/{request.Id}/override", new { notes = "urgent unblock" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<AccessRequestDto>();
        Assert.True(updated!.Resolution!.OverrideInvoked);
        Assert.Equal("HIERARCHICAL_MANAGEMENT_OVERRIDE", updated.Resolution.OverrideType);
    }

    [Fact]
    public async Task Resolving_twice_conflicts()
    {
        var request = await SeedPendingAsync("usr_analyst", "usr_lead");
        var client = Client("usr_lead", "Team Lead", "usr_director");
        await client.PostAsJsonAsync($"/api/access-requests/{request.Id}/approve", new { notes = "ok" });

        var second = await client.PostAsJsonAsync($"/api/access-requests/{request.Id}/reject", new { notes = "no" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Missing_request_is_not_found()
    {
        var client = Client("usr_lead", "Team Lead", "usr_director");

        var response = await client.PostAsJsonAsync("/api/access-requests/missing/approve", new { notes = "x" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<AccessRequest> SeedPendingAsync(string requester, string lead)
    {
        var store = _factory.Services.GetRequiredService<IAccessRequestStore>();
        return await store.CreateAsync(new AccessRequest(
            Guid.NewGuid().ToString("N"),
            "sess_seed",
            "[]",
            ["address.location.coordinates"],
            AccessRequest.PendingLead,
            null,
            DateTimeOffset.UnixEpoch,
            Requester: new RequesterInfo(requester, "Requester", "Business Analyst", lead),
            AssignedLeadId: lead,
            RequestedFlags: [new RequestedFlag("address.location.coordinates", "requires_approval")]));
    }

    private async Task<string> SeedPausedConversationAsync(string accessRequestId)
    {
        var store = _factory.Services.GetRequiredService<IConversationStore>();
        var state = new ConversationState(
            Guid.NewGuid().ToString("N"),
            "sess_seed",
            ConversationStep.Complete,
            "average coordinates near me",
            "[]",
            NlpRouteKind.GovernancePaused,
            accessRequestId,
            ["address.location.coordinates"],
            false,
            true,
            new ReportIntakeDraft(
                RequesterEmail: "analyst@enterprise.com",
                Purpose: "Geo analysis",
                BusinessImpact: "Target market expansion planning",
                ProjectCode: "PROJ-1",
                ManagerEmail: "manager@enterprise.com",
                Columns: ["name"],
                DeliveryFormat: "CSV"),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);
        await store.CreateAsync(state);
        return state.Id;
    }

    private HttpClient Client(string userId, string role, string? leadUserId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IssueToken(userId, role, leadUserId));
        return client;
    }

    private static string IssueToken(string userId, string role, string? leadUserId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GatewayFactory.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new("user_id", userId),
            new("name", userId),
            new("email", $"{userId}@enterprise.com"),
            new("role", role)
        };
        if (leadUserId is not null)
        {
            claims.Add(new Claim("lead_user_id", leadUserId));
        }

        var token = new JwtSecurityToken(
            issuer: GatewayFactory.Issuer,
            audience: GatewayFactory.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record AccessRequestDto(
        string Id,
        string Status,
        RequesterDto? Requester,
        ResolutionDto? Resolution);

    private sealed record RequesterDto(string UserId, string Name, string Role, string? LeadUserId);

    private sealed record ResolutionDto(
        string AssignedLeadId,
        string ResolvedByUserId,
        bool OverrideInvoked,
        string? OverrideType,
        string? Notes);

    private sealed record ConversationTurnDto(bool ApprovalRequired, bool Downloadable);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ApiGovernanceTests"
```

Expected: FAIL because the endpoints return 404.

- [ ] **Step 3: Add the endpoints**

In `src/gateway/Program.cs`, add `using Gateway.Nlp.Guardrails;` if missing (the file already imports `Gateway.Governance` and `Gateway.Conversations`). Insert these endpoints immediately before the `app.MapGet("/api/agents/stream", ...)` line:

```csharp
app.MapGet("/api/access-requests", async (
    string? status,
    ClaimsPrincipal user,
    IGovernanceService governance,
    CancellationToken cancellationToken) =>
{
    var viewer = SessionClaims.ToRequesterContext(user);
    var requests = await governance.ListAsync(viewer, status, cancellationToken);
    return Results.Ok(requests);
}).RequireAuthorization();

app.MapGet("/api/access-requests/{id}", async (
    string id,
    ClaimsPrincipal user,
    IGovernanceService governance,
    CancellationToken cancellationToken) =>
{
    try
    {
        var viewer = SessionClaims.ToRequesterContext(user);
        return Results.Ok(await governance.GetForViewerAsync(id, viewer, cancellationToken));
    }
    catch (GovernanceNotFoundException)
    {
        return Results.NotFound();
    }
    catch (GovernanceForbiddenException)
    {
        return Results.Forbid();
    }
}).RequireAuthorization();

app.MapPost("/api/access-requests/{id}/approve", async (
    string id,
    GovernanceDecisionRequest? body,
    ClaimsPrincipal user,
    IGovernanceService governance,
    CancellationToken cancellationToken) =>
{
    try
    {
        var actor = SessionClaims.ToRequesterContext(user);
        return Results.Ok(await governance.ApproveAsync(id, actor, body?.Notes, cancellationToken));
    }
    catch (GovernanceNotFoundException)
    {
        return Results.NotFound();
    }
    catch (GovernanceForbiddenException)
    {
        return Results.Forbid();
    }
    catch (GovernanceConflictException error)
    {
        return Results.Conflict(new { error = error.Message });
    }
}).RequireAuthorization();

app.MapPost("/api/access-requests/{id}/reject", async (
    string id,
    GovernanceDecisionRequest? body,
    ClaimsPrincipal user,
    IGovernanceService governance,
    CancellationToken cancellationToken) =>
{
    try
    {
        var actor = SessionClaims.ToRequesterContext(user);
        return Results.Ok(await governance.RejectAsync(id, actor, body?.Notes, cancellationToken));
    }
    catch (GovernanceNotFoundException)
    {
        return Results.NotFound();
    }
    catch (GovernanceForbiddenException)
    {
        return Results.Forbid();
    }
    catch (GovernanceConflictException error)
    {
        return Results.Conflict(new { error = error.Message });
    }
}).RequireAuthorization();

app.MapPost("/api/access-requests/{id}/override", async (
    string id,
    GovernanceDecisionRequest? body,
    ClaimsPrincipal user,
    IGovernanceService governance,
    CancellationToken cancellationToken) =>
{
    try
    {
        var actor = SessionClaims.ToRequesterContext(user);
        return Results.Ok(await governance.OverrideAsync(id, actor, body?.Notes ?? string.Empty, cancellationToken));
    }
    catch (GovernanceNotFoundException)
    {
        return Results.NotFound();
    }
    catch (GovernanceForbiddenException)
    {
        return Results.Forbid();
    }
    catch (GovernanceValidationException error)
    {
        return Results.BadRequest(new { error = error.Message });
    }
    catch (GovernanceConflictException error)
    {
        return Results.Conflict(new { error = error.Message });
    }
}).RequireAuthorization();
```

Add the request record near `ApprovalFlagRequest` at the bottom of `Program.cs`:

```csharp
public sealed record GovernanceDecisionRequest(string? Notes);
```

- [ ] **Step 4: Run the test to verify it passes**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ApiGovernanceTests"
```

Expected: PASS, 7 tests.

- [ ] **Step 5: Commit**

```bash
cd /workspace
git add src/gateway/Program.cs tests/Gateway.Tests/ApiGovernanceTests.cs
printf 'feat(api): expose the access request queue and decisions\n\nCo-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>\n' > /tmp/msg
git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg
```

---

### Task 10: Angular governance models and service

**Files:**
- Create: `src/web/src/app/models/governance.ts`
- Create: `src/web/src/app/services/governance.service.ts`
- Test: `src/web/src/app/services/governance.service.spec.ts`

**Interfaces:**
- Consumes: the endpoints from Task 9.
- Produces: `AccessRequest` and related interfaces; `GovernanceService.list/approve/reject/override`.

- [ ] **Step 1: Write the failing test**

Create `src/web/src/app/services/governance.service.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { GovernanceService } from './governance.service';

describe('GovernanceService', () => {
  let service: GovernanceService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(GovernanceService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists access requests, optionally filtered by status', () => {
    service.list('PENDING_LEAD').subscribe((requests) => expect(requests.length).toBe(1));

    const req = http.expectOne((request) => request.url === '/api/access-requests' && request.params.get('status') === 'PENDING_LEAD');
    expect(req.request.method).toBe('GET');
    req.flush([{ id: 'req_1', status: 'PENDING_LEAD' }]);
  });

  it('approves, rejects, and overrides', () => {
    service.approve('req_1', 'ok').subscribe();
    http.expectOne('/api/access-requests/req_1/approve').flush({ id: 'req_1' });

    service.reject('req_1').subscribe();
    http.expectOne('/api/access-requests/req_1/reject').flush({ id: 'req_1' });

    service.override('req_1', 'urgent').subscribe();
    const override = http.expectOne('/api/access-requests/req_1/override');
    expect(override.request.body).toEqual({ notes: 'urgent' });
    override.flush({ id: 'req_1' });
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
cd src/web
npm test -- --run 2>&1 | tail -20
```

Expected: FAIL because `governance.service.ts` does not exist.

- [ ] **Step 3: Create the models**

Create `src/web/src/app/models/governance.ts`:

```ts
export interface RequesterInfo {
  userId: string;
  name: string;
  role: string;
  leadUserId?: string | null;
}

export interface RequestedFlag {
  fieldPath: string;
  flag: string;
}

export interface GovernanceJustification {
  businessReason: string;
  businessImpact: string;
  projectCode?: string | null;
}

export interface ApprovalResolution {
  assignedLeadId: string;
  resolvedByUserId: string;
  resolvedByName: string;
  resolvedByRole: string;
  overrideInvoked: boolean;
  overrideType?: string | null;
  resolvedAt: string;
  notes?: string | null;
}

export interface AccessRequest {
  id: string;
  sessionId: string;
  mql: string;
  sensitiveFields: string[];
  status: string;
  justification?: string | null;
  createdAt: string;
  requester?: RequesterInfo | null;
  assignedLeadId?: string | null;
  requestedFlags?: RequestedFlag[] | null;
  justificationDetails?: GovernanceJustification | null;
  resolution?: ApprovalResolution | null;
}
```

- [ ] **Step 4: Create the service**

Create `src/web/src/app/services/governance.service.ts`:

```ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AccessRequest } from '../models/governance';

@Injectable({ providedIn: 'root' })
export class GovernanceService {
  constructor(private readonly http: HttpClient) {}

  list(status?: string): Observable<AccessRequest[]> {
    const params = status ? new HttpParams().set('status', status) : undefined;
    return this.http.get<AccessRequest[]>('/api/access-requests', { params });
  }

  approve(id: string, notes?: string): Observable<AccessRequest> {
    return this.http.post<AccessRequest>(`/api/access-requests/${id}/approve`, { notes: notes ?? null });
  }

  reject(id: string, notes?: string): Observable<AccessRequest> {
    return this.http.post<AccessRequest>(`/api/access-requests/${id}/reject`, { notes: notes ?? null });
  }

  override(id: string, notes: string): Observable<AccessRequest> {
    return this.http.post<AccessRequest>(`/api/access-requests/${id}/override`, { notes });
  }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run:

```bash
cd src/web
npm test -- --run 2>&1 | tail -20
```

Expected: PASS for the new spec.

- [ ] **Step 6: Commit**

```bash
cd /workspace
git add src/web/src/app/models/governance.ts src/web/src/app/services/governance.service.ts src/web/src/app/services/governance.service.spec.ts
printf 'feat(web): add governance models and service\n\nCo-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>\n' > /tmp/msg
git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg
```

---

### Task 11: Governance queue component and routing

**Files:**
- Create: `src/web/src/app/governance/governance-queue.component.ts`
- Create: `src/web/src/app/governance/governance-queue.component.html`
- Create: `src/web/src/app/governance/governance-queue.component.scss`
- Create: `src/web/src/app/workspace/workspace.component.ts`
- Create: `src/web/src/app/workspace/workspace.component.html`
- Create: `src/web/src/app/workspace/workspace.component.scss`
- Modify: `src/web/src/app/app.routes.ts`
- Modify: `src/web/src/app/app.component.ts`
- Modify: `src/web/src/app/app.component.html`
- Modify: `src/web/src/app/app.component.scss`
- Modify: `src/web/src/app/app.component.spec.ts`
- Test: `src/web/src/app/governance/governance-queue.component.spec.ts`
- Test: `src/web/src/app/workspace/workspace.component.spec.ts`

**Interfaces:**
- Consumes: `GovernanceService` (Task 10), `SessionService.role()`, the existing `ChatThreadComponent` and `GovernanceToggleComponent`.
- Produces: `/governance` route rendering `GovernanceQueueComponent`; `''` route rendering `WorkspaceComponent`; nav in `AppComponent`.

- [ ] **Step 1: Write the failing tests**

Create `src/web/src/app/governance/governance-queue.component.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { GovernanceQueueComponent } from './governance-queue.component';
import { GovernanceService } from '../services/governance.service';
import { SessionService } from '../services/session.service';

describe('GovernanceQueueComponent', () => {
  const pending = {
    id: 'req_1',
    sessionId: 'sess_1',
    mql: '[]',
    sensitiveFields: ['address.location.coordinates'],
    status: 'PENDING_LEAD',
    createdAt: '2026-09-25T00:00:00Z',
    requester: { userId: 'usr_analyst', name: 'Analyst', role: 'Business Analyst', leadUserId: 'usr_lead' },
    assignedLeadId: 'usr_lead',
    justificationDetails: { businessReason: 'Geo analysis', businessImpact: 'Market planning', projectCode: 'PROJ-1' }
  };

  let approved: string[];
  let overridden: string[];

  function configure(role: string) {
    approved = [];
    overridden = [];
    TestBed.configureTestingModule({
      imports: [GovernanceQueueComponent],
      providers: [
        {
          provide: GovernanceService,
          useValue: {
            list: () => of([pending]),
            approve: (id: string) => {
              approved.push(id);
              return of({ ...pending, status: 'APPROVED' });
            },
            reject: () => of({ ...pending, status: 'REJECTED' }),
            override: (id: string) => {
              overridden.push(id);
              return of({ ...pending, status: 'APPROVED' });
            }
          }
        },
        { provide: SessionService, useValue: { role: () => role } }
      ]
    });
  }

  it('renders pending requests and approves as the assigned lead', () => {
    configure('Team Lead');
    const fixture = TestBed.createComponent(GovernanceQueueComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Geo analysis');
    fixture.componentInstance.approve(pending);
    expect(approved).toEqual(['req_1']);
  });

  it('requires notes before overriding', () => {
    configure('Engineering Manager');
    const fixture = TestBed.createComponent(GovernanceQueueComponent);
    fixture.detectChanges();

    fixture.componentInstance.overrideNotes = '';
    fixture.componentInstance.override(pending);
    expect(overridden).toEqual([]);

    fixture.componentInstance.overrideNotes = 'urgent';
    fixture.componentInstance.override(pending);
    expect(overridden).toEqual(['req_1']);
  });
});
```

Create `src/web/src/app/workspace/workspace.component.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { WorkspaceComponent } from './workspace.component';
import { SessionService } from '../services/session.service';
import { AgentStreamService } from '../services/agent-stream.service';
import { ConversationService } from '../services/conversation.service';

describe('WorkspaceComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WorkspaceComponent],
      providers: [
        {
          provide: SessionService,
          useValue: {
            sessionId: () => 'sess_test',
            role: () => null,
            bootstrap: () => of({ token: 't' }),
            greeting: () => of({ displayName: 'Bikash', period: 'morning', message: 'Hi Bikash, Good morning', chips: ['Just run it'] })
          }
        },
        { provide: AgentStreamService, useValue: { connect: () => of({ event: 'agent.idle', data: '{}' }) } },
        { provide: ConversationService, useValue: { getApproval: () => of({ enabled: true }) } }
      ]
    }).compileComponents();
  });

  it('hosts the chat thread', () => {
    const fixture = TestBed.createComponent(WorkspaceComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('app-chat-thread')).toBeTruthy();
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:

```bash
cd src/web
npm test -- --run 2>&1 | tail -20
```

Expected: FAIL because the components do not exist.

- [ ] **Step 3: Extract the workspace component**

Create `src/web/src/app/workspace/workspace.component.ts`:

```ts
import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { retry, Subscription, switchMap, timer } from 'rxjs';
import { Greeting } from '../models/greeting';
import { AgentStreamService } from '../services/agent-stream.service';
import { SessionService } from '../services/session.service';
import { ChatThreadComponent } from '../chat/chat-thread.component';
import { GovernanceToggleComponent } from '../governance/governance-toggle.component';

@Component({
  selector: 'app-workspace',
  standalone: true,
  imports: [CommonModule, ChatThreadComponent, GovernanceToggleComponent],
  templateUrl: './workspace.component.html',
  styleUrl: './workspace.component.scss'
})
export class WorkspaceComponent implements OnInit, OnDestroy {
  greeting: Greeting | null = null;
  streamStatus = 'connecting';
  agentActivity: string[] = [];
  canManageGovernance = false;
  error: string | null = null;
  private sub = new Subscription();

  constructor(
    private readonly session: SessionService,
    private readonly agents: AgentStreamService
  ) {}

  ngOnInit(): void {
    this.session.sessionId();
    this.canManageGovernance = this.session.role() === 'Data Owner / Admin';
    this.sub.add(
      this.session.bootstrap().pipe(switchMap(() => this.session.greeting())).subscribe({
        next: (greeting) => {
          this.greeting = greeting;
          this.canManageGovernance = this.session.role() === 'Data Owner / Admin';
          this.listen();
        },
        error: () => {
          this.error = 'Unable to load session. Start the ASP.NET Core gateway on port 5235.';
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.sub.unsubscribe();
  }

  private listen(): void {
    this.sub.add(
      this.agents.connect().pipe(retry({ delay: () => timer(2000) })).subscribe({
        next: (event) => {
          this.streamStatus = event.event;
          this.agentActivity = [...this.agentActivity.slice(-4), event.event];
        },
        error: () => {
          this.streamStatus = 'reconnecting';
        }
      })
    );
  }
}
```

Create `src/web/src/app/workspace/workspace.component.html` with the existing `app.component.html` body unchanged (the `<main class="shell">` block).

Create `src/web/src/app/workspace/workspace.component.scss` with the existing `app.component.scss` contents unchanged.

- [ ] **Step 4: Reduce the app component to a shell with navigation**

Replace `src/web/src/app/app.component.ts` with:

```ts
import { Component } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {}
```

Replace `src/web/src/app/app.component.html` with:

```html
<nav class="nav">
  <a routerLink="/">Workspace</a>
  <a routerLink="/governance">Governance</a>
</nav>
<router-outlet></router-outlet>
```

Append to `src/web/src/app/app.component.scss`:

```scss
.nav {
  display: flex;
  gap: 1rem;
  padding: 0.75rem 1.5rem;
}
```

Replace `src/web/src/app/app.component.spec.ts` with:

```ts
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AppComponent } from './app.component';

describe('AppComponent', () => {
  it('renders navigation', async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [provideRouter([])]
    }).compileComponents();

    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('nav')).toBeTruthy();
    expect(compiled.textContent).toContain('Governance');
  });
});
```

- [ ] **Step 5: Create the governance queue component**

Create `src/web/src/app/governance/governance-queue.component.ts`:

```ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AccessRequest } from '../models/governance';
import { GovernanceService } from '../services/governance.service';
import { SessionService } from '../services/session.service';

@Component({
  selector: 'app-governance-queue',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './governance-queue.component.html',
  styleUrl: './governance-queue.component.scss'
})
export class GovernanceQueueComponent implements OnInit {
  requests: AccessRequest[] = [];
  overrideNotes = '';
  error: string | null = null;

  constructor(
    private readonly governance: GovernanceService,
    private readonly session: SessionService
  ) {}

  ngOnInit(): void {
    this.refresh();
  }

  get canDecide(): boolean {
    const role = this.session.role() ?? '';
    return (
      role === 'Team Lead' ||
      role === 'Engineering Manager' ||
      role === 'Director' ||
      role === 'Data Owner / Admin'
    );
  }

  refresh(): void {
    this.governance.list().subscribe({
      next: (requests) => {
        this.requests = requests;
        this.error = null;
      },
      error: () => (this.error = 'Unable to load the approval queue.')
    });
  }

  approve(request: AccessRequest): void {
    this.governance.approve(request.id).subscribe({
      next: () => this.refresh(),
      error: () => (this.error = 'Unable to approve this request.')
    });
  }

  reject(request: AccessRequest): void {
    this.governance.reject(request.id).subscribe({
      next: () => this.refresh(),
      error: () => (this.error = 'Unable to reject this request.')
    });
  }

  override(request: AccessRequest): void {
    const notes = this.overrideNotes.trim();
    if (!notes) {
      this.error = 'Override notes are required.';
      return;
    }

    this.governance.override(request.id, notes).subscribe({
      next: () => {
        this.overrideNotes = '';
        this.refresh();
      },
      error: () => (this.error = 'Unable to override this request.')
    });
  }
}
```

Create `src/web/src/app/governance/governance-queue.component.html`:

```html
<section class="queue">
  <h2>Governance approvals</h2>
  @if (error) {
    <p class="error" role="alert">{{ error }}</p>
  }
  @if (requests.length === 0) {
    <p>No requests in your queue.</p>
  }
  @for (request of requests; track request.id) {
    <article class="card" [attr.data-status]="request.status">
      <header>
        <h3>{{ request.requester?.name }}</h3>
        <span class="status">{{ request.status }}</span>
      </header>
      <p class="fields">Fields: {{ request.sensitiveFields.join(', ') }}</p>
      @if (request.justificationDetails; as justification) {
        <dl>
          <dt>Business reason</dt>
          <dd>{{ justification.businessReason }}</dd>
          <dt>Business impact</dt>
          <dd>{{ justification.businessImpact }}</dd>
          <dt>Project code</dt>
          <dd>{{ justification.projectCode || 'n/a' }}</dd>
        </dl>
      }
      @if (request.status === 'PENDING_LEAD' && canDecide) {
        <div class="actions">
          <button type="button" (click)="approve(request)">Approve</button>
          <button type="button" (click)="reject(request)">Reject</button>
          <input name="overrideNotes" placeholder="Override notes" [(ngModel)]="overrideNotes" />
          <button type="button" (click)="override(request)">Override</button>
        </div>
      }
    </article>
  }
</section>
```

Create `src/web/src/app/governance/governance-queue.component.scss`:

```scss
.queue {
  padding: 1.5rem;
  display: grid;
  gap: 1rem;
}
.card {
  border: 1px solid #d5d9e0;
  border-radius: 0.5rem;
  padding: 1rem;
}
.card header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.actions {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
  margin-top: 0.75rem;
}
.error {
  color: #b00020;
}
```

- [ ] **Step 6: Register the routes**

Replace `src/web/src/app/app.routes.ts` with:

```ts
import { Routes } from '@angular/router';
import { WorkspaceComponent } from './workspace/workspace.component';
import { GovernanceQueueComponent } from './governance/governance-queue.component';

export const routes: Routes = [
  { path: '', component: WorkspaceComponent },
  { path: 'governance', component: GovernanceQueueComponent }
];
```

- [ ] **Step 7: Run the web tests**

Run:

```bash
cd src/web
npm test -- --run 2>&1 | tail -20
```

Expected: PASS, including the new specs and the reduced app spec.

- [ ] **Step 8: Commit**

```bash
cd /workspace
git add src/web/src/app/app.routes.ts src/web/src/app/app.component.ts src/web/src/app/app.component.html src/web/src/app/app.component.scss src/web/src/app/app.component.spec.ts src/web/src/app/governance/governance-queue.component.ts src/web/src/app/governance/governance-queue.component.html src/web/src/app/governance/governance-queue.component.scss src/web/src/app/governance/governance-queue.component.spec.ts src/web/src/app/workspace
printf 'feat(web): add the governance queue and routing\n\nCo-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>\n' > /tmp/msg
git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg
```

---

### Task 12: Chat business-impact control and justification summary

**Files:**
- Modify: `src/web/src/app/models/conversation.ts`
- Modify: `src/web/src/app/chat/chat-thread.component.ts`
- Modify: `src/web/src/app/chat/chat-thread.component.html`
- Modify: `src/web/src/app/chat/chat-thread.component.spec.ts`
- Test: `src/web/src/app/chat/chat-thread.component.spec.ts`

**Interfaces:**
- Consumes: `ConversationAnswer` from Task 10 and the backend `BusinessImpact` control from Task 6.
- Produces: a `businessimpact` control that submits `{ businessImpact }`, and a justification summary on completion.

- [ ] **Step 1: Write the failing test**

Add to `src/web/src/app/chat/chat-thread.component.spec.ts` (keep existing tests):

```ts
  it('submits the business impact answer', () => {
    const fixture = TestBed.createComponent(ChatThreadComponent);
    const component = fixture.componentInstance;
    let captured: any = null;
    const conversations = TestBed.inject(ConversationService) as any;
    conversations.answer = (_id: string, answer: any) => {
      captured = answer;
      return of({ conversationId: 'c1', step: 'ManagerEmail', kind: 'GovernancePaused', assistantMessage: 'Manager?', control: 'none', deliveryOptions: ['EMAIL', 'CSV'], approvalRequired: true, downloadable: false, demoReport: false });
    };
    component.turn = { conversationId: 'c1', step: 'BusinessImpact', kind: 'GovernancePaused', assistantMessage: 'Impact?', control: 'businessimpact', deliveryOptions: ['EMAIL', 'CSV'], approvalRequired: true, downloadable: false, demoReport: false } as any;

    component.businessImpact = 'Target market expansion planning';
    component.submitBusinessImpact();

    expect(captured).toEqual({ businessImpact: 'Target market expansion planning' });
  });
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
cd src/web
npm test -- --run 2>&1 | tail -20
```

Expected: FAIL because `businessImpact` and `submitBusinessImpact` do not exist.

- [ ] **Step 3: Extend the models**

In `src/web/src/app/models/conversation.ts`, add `businessImpact?: string;` to `ConversationAnswer` after `projectCode?: string;`.

- [ ] **Step 4: Extend the component**

In `src/web/src/app/chat/chat-thread.component.ts`, add a field after `projectCode = '';`:

```ts
  businessImpact = '';
```

Add a submit method after `submitPurpose()`:

```ts
  submitBusinessImpact(): void {
    this.answer({ businessImpact: this.businessImpact.trim() });
  }
```

In the `answer` echo chain, add a branch before `else if (answer.managerEmail)`:

```ts
        } else if (answer.businessImpact) {
          this.messages = [...this.messages, { role: 'user', text: answer.businessImpact }];
```

And in `apply`, after the columns branch, reset the business impact input when moving on:

```ts
    if (turn.control === 'managementemail') {
      this.businessImpact = '';
    }
```

- [ ] **Step 5: Extend the template**

In `src/web/src/app/chat/chat-thread.component.html`, add this block after the `purpose` form block and before the `columns` block:

```html
  } @else if (turn?.control === 'businessimpact') {
    <form class="step" (ngSubmit)="submitBusinessImpact()">
      <label for="step-impact">Business impact</label>
      <textarea id="step-impact" name="businessImpact" [(ngModel)]="businessImpact" required></textarea>
      <button type="submit" [disabled]="pending">Confirm impact</button>
    </form>
```

- [ ] **Step 6: Run the web tests**

Run:

```bash
cd src/web
npm test -- --run 2>&1 | tail -20
```

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
cd /workspace
git add src/web/src/app/models/conversation.ts src/web/src/app/chat/chat-thread.component.ts src/web/src/app/chat/chat-thread.component.html src/web/src/app/chat/chat-thread.component.spec.ts
printf 'feat(web): capture the business impact in the chat\n\nCo-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>\n' > /tmp/msg
git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg
```

---

### Task 13: Documentation, acceptance ticks, and full verification

**Files:**
- Modify: `README.md`
- Modify: `sprints/sprint-5.md`
- Verify: full .NET and web suites; restore benchmarks.

**Interfaces:**
- Consumes: everything above.
- Produces: documented governance workflow; ticked Sprint 5 acceptance criteria; a green full suite.

- [ ] **Step 1: Document the workflow**

In `README.md`, add a "Governance approvals" section that covers: Data Owner exemption, Team Lead queue with approve/reject, manager/director override with required notes, the `/governance` route, and the `PENDING_LEAD` to `APPROVED`/`REJECTED` lifecycle, noting that state is in-memory.

- [ ] **Step 2: Tick the acceptance criteria**

In `sprints/sprint-5.md`, change each `- [ ]` in the Acceptance criteria section to `- [x]`.

- [ ] **Step 3: Run the full .NET suite**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test tests/Gateway.Tests/Gateway.Tests.csproj
```

Expected: all tests PASS, 0 failed.

- [ ] **Step 4: Restore generated benchmarks**

Run:

```bash
cd /workspace
git restore docs/benchmarks/
git status --porcelain docs/benchmarks/
```

Expected: no output.

- [ ] **Step 5: Run the web suite**

Run:

```bash
cd src/web
npm test -- --run 2>&1 | tail -20
```

Expected: all tests PASS, 0 failed.

- [ ] **Step 6: Confirm no stray files**

Run:

```bash
cd /workspace
git status --porcelain
```

Expected: only `README.md`, `sprints/sprint-5.md`, and `src/web/.vscode/` (untracked, must not be staged). If `src/web/.vscode/` is listed, leave it out of the commit.

- [ ] **Step 7: Commit**

```bash
cd /workspace
git add README.md sprints/sprint-5.md
printf 'docs(governance): document approvals and tick sprint 5 criteria\n\nCo-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>\n' > /tmp/msg
git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg
```

---

## Self-Review

**Spec coverage:** exemption (Task 3), justification in chat (Task 6), lifecycle and override (Task 2), queue scoping and authority (Task 2, Task 9), resume (Task 5), requester context (Task 1, Task 4, Task 7), API surface (Task 9), SPA portal and routing (Task 10, Task 11), chat control (Task 12), acceptance and docs (Task 13). All spec sections map to a task.

**Placeholder scan:** none. Every code step contains complete code.

**Type consistency:** `RequesterContext` is created in Task 1 and consumed unchanged in Tasks 2, 4, 7, 8, 9. `IGovernanceService` members are declared in Task 2 and called in Task 9 with the same signatures. `IConversationResumeHandler.ResumeAsync` is declared in Task 2 and implemented in Task 5. `GovernanceJustification` is declared in Task 2 and built in Task 6. `BusinessImpact` is added to the backend in Task 6 and to the SPA in Task 12. `GovernanceService.list/approve/reject/override` match the endpoints and the component calls.

**Cycle check:** `NlpOrchestrator` and `ConversationOrchestrator` depend on `IAccessRequestStore`; only `GovernanceService` depends on `IConversationResumeHandler`. The spec's earlier wording said the orchestrator would call `IGovernanceService`; this plan keeps `IAccessRequestStore` there to avoid a `GovernanceService -> IConversationResumeHandler -> ConversationOrchestrator -> INlpOrchestrator -> IGovernanceService` cycle. The design doc has been updated to match.
