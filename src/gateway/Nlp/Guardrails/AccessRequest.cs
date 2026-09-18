namespace Gateway.Nlp.Guardrails;

public sealed record AccessRequest(
    string Id,
    string SessionId,
    string Mql,
    IReadOnlyList<string> SensitiveFields,
    string Status,
    string? Justification,
    DateTimeOffset CreatedAt)
{
    public const string PendingLead = "PENDING_LEAD";
}
