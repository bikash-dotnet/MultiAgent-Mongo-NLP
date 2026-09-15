using Gateway.Nlp.Intent;
using Gateway.Nlp.Mql;

namespace Gateway.Nlp.Router;

public enum NlpRouteKind
{
    CacheHit,
    SimpleMql,
    ClarifyRequired,
    ComplexLlmRequired,
    ComplexLlmFailed
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
    string? Error = null);
