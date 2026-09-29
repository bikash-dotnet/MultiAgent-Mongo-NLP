using Gateway.Nlp.Orchestrator;

namespace Gateway.Persistence;

public sealed class DurableAgentStateStore : IAgentStateStore
{
    private const string Collection = "agent_states";

    private readonly IDocumentStore _store;

    public DurableAgentStateStore(IDocumentStore store)
    {
        _store = store;
    }

    public Task SaveAsync(AgentState state, CancellationToken cancellationToken = default)
    {
        return _store.UpsertAsync(Collection, state.SessionId, state, cancellationToken);
    }

    public Task<AgentState?> LoadAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return _store.GetAsync<AgentState>(Collection, sessionId, cancellationToken);
    }
}
