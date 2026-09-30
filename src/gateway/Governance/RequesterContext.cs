namespace Gateway.Governance;

public sealed record RequesterContext(string UserId, string Name, string Role, string? LeadUserId, string? Email = null);
