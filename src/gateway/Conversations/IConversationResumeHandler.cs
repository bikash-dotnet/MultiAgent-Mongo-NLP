namespace Gateway.Conversations;

public interface IConversationResumeHandler
{
    Task<bool> ResumeAsync(string accessRequestId, CancellationToken cancellationToken = default);
}
