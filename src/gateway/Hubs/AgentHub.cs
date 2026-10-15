using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Gateway.Hubs;

[Authorize]
public sealed class AgentHub : Hub<IAgentHubClient>
{
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.ReceiveAgentEvent("agent.idle", "idle", "idle");
        await base.OnConnectedAsync();
    }
}
