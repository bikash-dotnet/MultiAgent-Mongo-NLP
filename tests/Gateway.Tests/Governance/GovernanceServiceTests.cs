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
