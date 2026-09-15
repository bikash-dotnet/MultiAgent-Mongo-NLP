namespace Gateway.Nlp.Orchestrator;

public sealed record AgentState(
    string SessionId,
    string Stage,
    string? PendingQuestion,
    DateTimeOffset UpdatedAt);
