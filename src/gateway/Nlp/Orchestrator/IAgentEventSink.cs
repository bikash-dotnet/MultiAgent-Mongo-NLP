namespace Gateway.Nlp.Orchestrator;

public interface IAgentEventSink
{
    void Publish(AgentEvent agentEvent);

    IAsyncEnumerable<AgentEvent> ReadAllAsync(CancellationToken cancellationToken);
}
