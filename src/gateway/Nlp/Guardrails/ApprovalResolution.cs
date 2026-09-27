namespace Gateway.Nlp.Guardrails;

public sealed record ApprovalResolution(
    string AssignedLeadId,
    string ResolvedByUserId,
    string ResolvedByName,
    string ResolvedByRole,
    bool OverrideInvoked,
    string? OverrideType,
    DateTimeOffset ResolvedAt,
    string? Notes)
{
    public const string HierarchicalManagementOverride = "HIERARCHICAL_MANAGEMENT_OVERRIDE";
}
