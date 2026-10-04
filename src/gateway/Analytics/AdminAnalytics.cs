using Gateway.Audit;

namespace Gateway.Analytics;

public static class AdminAnalytics
{
    public static AdminAnalyticsMetrics Aggregate(IReadOnlyList<AuditLogDocument> documents)
    {
        var total = documents.Count;
        var hits = documents.Count(document => document.NlpPerformance.SemanticCacheHit);
        var tokens = documents.Sum(document => document.NlpPerformance.LlmTokensConsumed);
        var sensitive = documents.Count(document => document.Governance.SensitiveDataAccessed);
        var overrides = documents.Count(document => document.Governance.OverrideInvoked);

        var durations = documents
            .Select(document => document.NlpPerformance.ExecutionDurationMs)
            .OrderBy(duration => duration)
            .ToList();
        var p95 = durations.Count == 0
            ? 0
            : durations[(int)Math.Ceiling(0.95 * durations.Count) - 1];

        var exports = documents
            .Where(document => !string.IsNullOrWhiteSpace(document.ExecutionDetails.ExportFormat))
            .GroupBy(document => document.ExecutionDetails.ExportFormat!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var sources = documents
            .GroupBy(document => document.DataSource, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var last = total == 0 ? (DateTimeOffset?)null : documents.Max(document => document.AuditTimestamp);
        var hitRate = total == 0 ? 0d : (double)hits / total;

        return new AdminAnalyticsMetrics(
            total,
            hits,
            hitRate,
            tokens,
            sensitive,
            overrides,
            p95,
            exports,
            sources,
            last);
    }
}
