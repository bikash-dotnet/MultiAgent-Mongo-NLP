using Gateway.Nlp.Router;

namespace Gateway.Nlp.Http;

public sealed record NlpQueryResponse(
    string Kind,
    string? Mql,
    string? Question,
    bool SemanticCacheHit,
    bool SlotExtractionUsed,
    string Intent,
    bool JustRunIt,
    int LlmTokensConsumed,
    int LlmAttempts,
    string? Error,
    IReadOnlyList<string>? SensitiveFields,
    string? AccessRequestId,
    string? GuardrailReason,
    IReadOnlyList<string>? Columns = null,
    IReadOnlyList<IReadOnlyDictionary<string, string?>>? Rows = null,
    string? DataSource = null,
    int? RowCount = null,
    long? DurationMs = null,
    string? ExecutionError = null)
{
    public static NlpQueryResponse From(NlpRouteResult result)
    {
        return new NlpQueryResponse(
            result.Kind.ToString(),
            result.Mql,
            result.Question,
            result.SemanticCacheHit,
            result.SlotExtractionUsed,
            result.Intent.ToString(),
            result.JustRunIt,
            result.LlmTokensConsumed,
            result.LlmAttempts,
            result.Error,
            result.SensitiveFields,
            result.AccessRequestId,
            result.GuardrailReason,
            result.Columns,
            result.Rows,
            result.DataSource,
            result.RowCount,
            result.DurationMs,
            result.ExecutionError);
    }
}
