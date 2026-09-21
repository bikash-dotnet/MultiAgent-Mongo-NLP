namespace Gateway.Governance;

public sealed class InMemoryApprovalFlagStore : IApprovalFlagStore
{
    private int _enabled;

    public InMemoryApprovalFlagStore(bool initialEnabled = true)
    {
        _enabled = initialEnabled ? 1 : 0;
    }

    public bool Enabled => Volatile.Read(ref _enabled) == 1;

    public Task<bool> SetAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        Volatile.Write(ref _enabled, enabled ? 1 : 0);
        return Task.FromResult(enabled);
    }
}
