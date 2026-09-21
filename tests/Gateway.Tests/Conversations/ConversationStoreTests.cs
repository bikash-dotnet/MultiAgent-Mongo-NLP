using Gateway.Conversations;
using Gateway.Nlp.Http;
using Gateway.Nlp.Router;

namespace Gateway.Tests.Conversations;

public class ConversationStoreTests
{
    [Fact]
    public async Task Creates_reads_and_updates_a_conversation()
    {
        var store = new InMemoryConversationStore();
        var state = new ConversationState(
            string.Empty,
            "sess_1",
            ConversationStep.Email,
            "listings with pools",
            "[{\"$match\":{}}]",
            NlpRouteKind.ComplexLlmRequired,
            null,
            [],
            false,
            false,
            new ReportIntakeDraft(),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);

        var created = await store.CreateAsync(state);
        Assert.False(string.IsNullOrWhiteSpace(created.Id));

        var fetched = await store.GetAsync(created.Id);
        Assert.Equal(ConversationStep.Email, fetched!.Step);

        await store.UpdateAsync(fetched with { Step = ConversationStep.Purpose });

        var updated = await store.GetAsync(created.Id);
        Assert.Equal(ConversationStep.Purpose, updated!.Step);
    }

    [Fact]
    public async Task Missing_ids_return_null()
    {
        var store = new InMemoryConversationStore();

        Assert.Null(await store.GetAsync("missing"));
    }
}
