namespace Gateway.Hubs;

public interface IAgentHubClient
{
    Task ReceiveAgentEvent(string eventName, string status, string detail);
}
