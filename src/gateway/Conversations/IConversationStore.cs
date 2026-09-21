namespace Gateway.Conversations;

public interface IConversationStore
{
    Task<ConversationState> CreateAsync(ConversationState state, CancellationToken cancellationToken = default);

    Task<ConversationState?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task<ConversationState> UpdateAsync(ConversationState state, CancellationToken cancellationToken = default);
}
