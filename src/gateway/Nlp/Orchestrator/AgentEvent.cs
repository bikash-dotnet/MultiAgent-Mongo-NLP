namespace Gateway.Nlp.Orchestrator;

public sealed record AgentEvent(string Name, string Status, string? Detail = null);
