namespace Gateway.Nlp.Orchestrator;

public interface IAgentStateStore
{
    Task SaveAsync(AgentState state, CancellationToken cancellationToken = default);
    Task<AgentState?> LoadAsync(string sessionId, CancellationToken cancellationToken = default);
}
