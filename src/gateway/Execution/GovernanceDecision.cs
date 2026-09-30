namespace Gateway.Execution;

public sealed record GovernanceDecision(
    bool SensitiveDataAccessed,
    IReadOnlyList<string> FlagsTriggered,
    string? ExemptionType,
    bool OverrideInvoked,
    string? AuthorizedBy)
{
    public static GovernanceDecision None { get; } = new(false, [], null, false, null);
}
