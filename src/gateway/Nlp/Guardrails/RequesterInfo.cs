namespace Gateway.Nlp.Guardrails;

public sealed record RequesterInfo(string UserId, string Name, string Role, string? LeadUserId);
