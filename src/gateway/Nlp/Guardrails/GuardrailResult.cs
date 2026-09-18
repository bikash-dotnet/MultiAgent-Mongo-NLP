namespace Gateway.Nlp.Guardrails;

public sealed record GuardrailResult(
    GuardrailOutcome Outcome,
    string? Reason,
    IReadOnlyList<string> SensitiveFields,
    IReadOnlyList<string> UnknownFields,
    IReadOnlyList<string> BlockedOperators)
{
    public static readonly GuardrailResult Allowed = new(GuardrailOutcome.Allowed, null, [], [], []);
}
