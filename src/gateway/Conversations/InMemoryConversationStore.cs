using System.Collections.Concurrent;

namespace Gateway.Conversations;

public sealed class InMemoryConversationStore : IConversationStore
{
    private readonly ConcurrentDictionary<string, ConversationState> _states = new(StringComparer.Ordinal);

    public Task<ConversationState> CreateAsync(ConversationState state, CancellationToken cancellationToken = default)
    {
        var stored = string.IsNullOrWhiteSpace(state.Id)
            ? state with { Id = Guid.NewGuid().ToString("N") }
            : state;

        _states[stored.Id] = stored;
        return Task.FromResult(stored);
    }

    public Task<ConversationState?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        _states.TryGetValue(id, out var state);
        return Task.FromResult(state);
    }

    public Task<ConversationState> UpdateAsync(ConversationState state, CancellationToken cancellationToken = default)
    {
        _states[state.Id] = state;
        return Task.FromResult(state);
    }
}
