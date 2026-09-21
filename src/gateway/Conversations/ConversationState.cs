using Gateway.Nlp.Http;
using Gateway.Nlp.Router;

namespace Gateway.Conversations;

public sealed record ConversationState(
    string Id,
    string SessionId,
    ConversationStep Step,
    string Utterance,
    string? Mql,
    NlpRouteKind Kind,
    string? AccessRequestId,
    IReadOnlyList<string> SensitiveFields,
    bool DemoReport,
    bool ApprovalRequired,
    ReportIntakeDraft Draft,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? Message = null,
    NlpQueryResponse? Result = null);
