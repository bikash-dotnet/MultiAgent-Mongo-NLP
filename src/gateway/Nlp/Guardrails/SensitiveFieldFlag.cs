namespace Gateway.Nlp.Guardrails;

public sealed record SensitiveFieldFlag(
    string Path,
    bool IsSensitive,
    bool RequiresApproval,
    IReadOnlyList<string> DataOwnerRoles);
