namespace Gateway.Conversations;

public sealed record ConversationAnswer(
    string? Text = null,
    string? Email = null,
    string? Purpose = null,
    string? ProjectCode = null,
    string? ManagerEmail = null,
    IReadOnlyList<string>? Columns = null,
    string? Delivery = null);
