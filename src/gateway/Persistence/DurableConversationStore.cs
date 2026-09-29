using Gateway.Conversations;

namespace Gateway.Persistence;

public sealed class DurableConversationStore : IConversationStore
{
    private const string Collection = "conversations";

    private readonly IDocumentStore _store;

    public DurableConversationStore(IDocumentStore store)
    {
        _store = store;
    }

    public async Task<ConversationState> CreateAsync(ConversationState state, CancellationToken cancellationToken = default)
    {
        var stored = string.IsNullOrWhiteSpace(state.Id)
            ? state with { Id = Guid.NewGuid().ToString("N") }
            : state;

        await _store.UpsertAsync(Collection, stored.Id, stored, cancellationToken);
        return stored;
    }

    public Task<ConversationState?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        return _store.GetAsync<ConversationState>(Collection, id, cancellationToken);
    }

    public async Task<ConversationState?> FindByAccessRequestAsync(string accessRequestId, CancellationToken cancellationToken = default)
    {
        var all = await _store.GetAllAsync<ConversationState>(Collection, cancellationToken);
        return all.FirstOrDefault(state => state.AccessRequestId == accessRequestId);
    }

    public Task<ConversationState> UpdateAsync(ConversationState state, CancellationToken cancellationToken = default)
    {
        return UpsertAndReturn(state, cancellationToken);
    }

    private async Task<ConversationState> UpsertAndReturn(ConversationState state, CancellationToken cancellationToken)
    {
        await _store.UpsertAsync(Collection, state.Id, state, cancellationToken);
        return state;
    }
}
