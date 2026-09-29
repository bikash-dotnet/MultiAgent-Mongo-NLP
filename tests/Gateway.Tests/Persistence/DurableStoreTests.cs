using Gateway.Conversations;
using Gateway.Nlp.Guardrails;
using Gateway.Nlp.Orchestrator;
using Gateway.Persistence;

namespace Gateway.Tests.Persistence;

public class DurableStoreTests
{
    [Fact]
    public async Task Agent_state_survives_a_new_store_instance()
    {
        var backend = new InMemoryDocumentStore();
        var saved = new AgentState("session-1", "governance_paused", "needs approval", DateTimeOffset.UnixEpoch, "req-1");

        await new DurableAgentStateStore(backend).SaveAsync(saved, CancellationToken.None);

        var reloaded = await new DurableAgentStateStore(backend).LoadAsync("session-1", CancellationToken.None);

        Assert.Equal("governance_paused", reloaded!.Stage);
        Assert.Equal("req-1", reloaded.AccessRequestId);
    }

    [Fact]
    public async Task Access_request_survives_and_updates_in_place()
    {
        var backend = new InMemoryDocumentStore();
        var request = new AccessRequest(string.Empty, "session-1", "[]", ["price"], AccessRequest.PendingLead, null, DateTimeOffset.UnixEpoch);

        var created = await new DurableAccessRequestStore(backend).CreateAsync(request, CancellationToken.None);
        await new DurableAccessRequestStore(backend).UpdateAsync(created with { Status = AccessRequest.Approved }, CancellationToken.None);

        var reloaded = await new DurableAccessRequestStore(backend).GetAsync(created.Id, CancellationToken.None);

        Assert.Equal(AccessRequest.Approved, reloaded!.Status);
    }

    [Fact]
    public async Task Conversation_is_found_by_access_request_after_reload()
    {
        var backend = new InMemoryDocumentStore();
        var state = new ConversationState(
            string.Empty, "session-1", ConversationStep.Complete, "q", "[{\"$match\":{}}]", Gateway.Nlp.Router.NlpRouteKind.GovernancePaused,
            "req-9", ["price"], false, true, new ReportIntakeDraft(), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

        var created = await new DurableConversationStore(backend).CreateAsync(state, CancellationToken.None);
        var found = await new DurableConversationStore(backend).FindByAccessRequestAsync("req-9", CancellationToken.None);

        Assert.Equal(created.Id, found!.Id);
    }
}
