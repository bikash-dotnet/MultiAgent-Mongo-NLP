namespace Gateway.Endpoints;

public sealed record ApprovalFlagRequest(bool Enabled);

public sealed record GovernanceDecisionRequest(string? Notes);

public sealed record EmailBriefingRequest(string? Recipient);

public sealed record EnterpriseCoreQueryRequest(string? Query, string? Collection, IReadOnlyList<string>? Columns);
