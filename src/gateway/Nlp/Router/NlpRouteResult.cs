using Gateway.Nlp.Intent;
using Gateway.Nlp.Mql;

namespace Gateway.Nlp.Router;

public enum NlpRouteKind
{
    CacheHit,
    SimpleMql,
    ClarifyRequired,
    ComplexLlmRequired,
    ComplexLlmFailed,
    GovernancePaused,
    Rejected
}

public sealed record NlpRouteResult(
    NlpRouteKind Kind,
    string? Mql,
    string? Question,
    bool SemanticCacheHit,
    bool SlotExtractionUsed,
    IntentKind Intent,
    bool JustRunIt,
    MqlDefaults ClarificationsApplied,
    int LlmTokensConsumed,
    int LlmAttempts = 0,
    string? Error = null,
    IReadOnlyList<string>? SensitiveFields = null,
    string? AccessRequestId = null,
    string? GuardrailReason = null,
    IReadOnlyList<string>? Columns = null,
    IReadOnlyList<IReadOnlyDictionary<string, string?>>? Rows = null,
    string? DataSource = null,
    int? RowCount = null,
    long? DurationMs = null,
    string? ExecutionError = null);
