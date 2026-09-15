using System.Collections.Concurrent;

namespace Gateway.Nlp.Orchestrator;

public sealed class InMemoryAgentStateStore : IAgentStateStore
{
    private readonly ConcurrentDictionary<string, AgentState> _states = new();

    public Task SaveAsync(AgentState state, CancellationToken cancellationToken = default)
    {
        _states[state.SessionId] = state;
        return Task.CompletedTask;
    }

    public Task<AgentState?> LoadAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        _states.TryGetValue(sessionId, out var state);
        return Task.FromResult(state);
    }
}
