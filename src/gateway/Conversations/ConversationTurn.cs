using Gateway.Nlp.Http;

namespace Gateway.Conversations;

public sealed record ConversationTurn(
    string ConversationId,
    string Step,
    string Kind,
    string AssistantMessage,
    string Control,
    string? EmailPrefill,
    IReadOnlyList<ColumnOption>? Columns,
    IReadOnlyList<string> DeliveryOptions,
    string? AccessRequestId,
    bool ApprovalRequired,
    bool Downloadable,
    bool DemoReport,
    NlpQueryResponse? Result,
    string? ValidationError);
