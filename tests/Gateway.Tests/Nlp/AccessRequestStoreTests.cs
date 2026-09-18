using Gateway.Nlp.Guardrails;

namespace Gateway.Tests.Nlp;

public class AccessRequestStoreTests
{
    [Fact]
    public async Task Create_assigns_id_and_round_trips()
    {
        var store = new InMemoryAccessRequestStore();
        var request = new AccessRequest(
            string.Empty,
            "user-1",
            """[{"$match":{}}]""",
            ["address.location.coordinates"],
            AccessRequest.PendingLead,
            "Need coordinates for a report",
            DateTimeOffset.UtcNow);

        var stored = await store.CreateAsync(request);

        Assert.False(string.IsNullOrWhiteSpace(stored.Id));
        Assert.Equal(AccessRequest.PendingLead, stored.Status);

        var loaded = await store.GetAsync(stored.Id);
        Assert.NotNull(loaded);
        Assert.Equal("user-1", loaded!.SessionId);
    }

    [Fact]
    public async Task Get_missing_returns_null()
    {
        var store = new InMemoryAccessRequestStore();

        Assert.Null(await store.GetAsync("does-not-exist"));
    }
}
