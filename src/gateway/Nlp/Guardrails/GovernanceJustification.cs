namespace Gateway.Nlp.Guardrails;

public sealed record GovernanceJustification(
    string BusinessReason,
    string BusinessImpact,
    string? ProjectCode);
