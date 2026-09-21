namespace Gateway.Governance;

public sealed class GovernanceOptions
{
    public const string SectionName = "Governance";

    public bool ApprovalEnabled { get; set; } = true;
}
