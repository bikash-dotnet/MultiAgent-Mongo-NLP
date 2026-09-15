using Gateway.Nlp.Orchestrator;

namespace Gateway.Tests.Nlp;

public class AgentStateStoreTests
{
    [Fact]
    public async Task Save_then_load_round_trips_state()
    {
        var store = new InMemoryAgentStateStore();
        var state = new AgentState("sess_1", "paused", "Awaiting governance approval", DateTimeOffset.UnixEpoch);

        await store.SaveAsync(state);
        var loaded = await store.LoadAsync("sess_1");

        Assert.Equal(state, loaded);
    }

    [Fact]
    public async Task Load_unknown_session_returns_null()
    {
        var store = new InMemoryAgentStateStore();

        Assert.Null(await store.LoadAsync("missing"));
    }
}
