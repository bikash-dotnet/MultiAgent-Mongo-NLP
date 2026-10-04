namespace Gateway.Analytics;

public sealed record AdminAnalyticsMetrics(
    int TotalExecutions,
    int SemanticCacheHits,
    double SemanticCacheHitRate,
    int TotalLlmTokens,
    int SensitiveAccessCount,
    int OverrideCount,
    double P95DurationMs,
    IReadOnlyDictionary<string, int> ExportCounts,
    IReadOnlyDictionary<string, int> DataSourceCounts,
    DateTimeOffset? LastExecutionAt);
