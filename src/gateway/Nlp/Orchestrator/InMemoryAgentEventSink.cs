using System.Threading.Channels;
using Gateway.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Gateway.Nlp.Orchestrator;

public sealed class InMemoryAgentEventSink : IAgentEventSink
{
    private readonly Channel<AgentEvent> _channel = Channel.CreateUnbounded<AgentEvent>(
        new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
    private readonly IHubContext<AgentHub, IAgentHubClient>? _hubContext;

    public InMemoryAgentEventSink(IHubContext<AgentHub, IAgentHubClient>? hubContext = null)
    {
        _hubContext = hubContext;
    }

    public void Publish(AgentEvent agentEvent)
    {
        _channel.Writer.TryWrite(agentEvent);
        _hubContext?.Clients.All.ReceiveAgentEvent(agentEvent.Name, agentEvent.Status, agentEvent.Detail ?? string.Empty);
    }

    public IAsyncEnumerable<AgentEvent> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
