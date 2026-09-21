using Gateway.Governance;

namespace Gateway.Tests.Governance;

public class ApprovalFlagStoreTests
{
    [Fact]
    public void Defaults_to_the_initial_value()
    {
        Assert.True(new InMemoryApprovalFlagStore(true).Enabled);
        Assert.False(new InMemoryApprovalFlagStore(false).Enabled);
    }

    [Fact]
    public async Task Set_flips_the_value_at_runtime()
    {
        var store = new InMemoryApprovalFlagStore(true);

        await store.SetAsync(false);

        Assert.False(store.Enabled);

        await store.SetAsync(true);

        Assert.True(store.Enabled);
    }
}
