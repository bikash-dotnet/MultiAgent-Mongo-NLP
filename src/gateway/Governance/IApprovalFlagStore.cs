namespace Gateway.Governance;

public interface IApprovalFlagStore
{
    bool Enabled { get; }

    Task<bool> SetAsync(bool enabled, CancellationToken cancellationToken = default);
}
