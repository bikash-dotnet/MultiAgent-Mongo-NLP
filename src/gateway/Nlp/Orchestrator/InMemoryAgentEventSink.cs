using System.Threading.Channels;

namespace Gateway.Nlp.Orchestrator;

public sealed class InMemoryAgentEventSink : IAgentEventSink
{
    private readonly Channel<AgentEvent> _channel = Channel.CreateUnbounded<AgentEvent>(
        new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

    public void Publish(AgentEvent agentEvent)
    {
        _channel.Writer.TryWrite(agentEvent);
    }

    public IAsyncEnumerable<AgentEvent> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
