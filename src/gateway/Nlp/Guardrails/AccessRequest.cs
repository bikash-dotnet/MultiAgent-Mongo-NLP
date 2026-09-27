namespace Gateway.Nlp.Guardrails;

public sealed record AccessRequest(
    string Id,
    string SessionId,
    string Mql,
    IReadOnlyList<string> SensitiveFields,
    string Status,
    string? Justification,
    DateTimeOffset CreatedAt,
    ReportIntake? Intake = null,
    RequesterInfo? Requester = null,
    string? AssignedLeadId = null,
    IReadOnlyList<RequestedFlag>? RequestedFlags = null,
    GovernanceJustification? JustificationDetails = null,
    ApprovalResolution? Resolution = null)
{
    public const string PendingLead = "PENDING_LEAD";

    public const string Approved = "APPROVED";

    public const string Rejected = "REJECTED";

    public const string ExemptionOwnerAccess = "EXEMPTION_OWNER_ACCESS";
}
