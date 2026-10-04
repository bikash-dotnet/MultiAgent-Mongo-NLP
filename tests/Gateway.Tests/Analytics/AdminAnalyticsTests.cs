using Gateway.Analytics;
using Gateway.Audit;

namespace Gateway.Tests.Analytics;

public class AdminAnalyticsTests
{
    [Fact]
    public void Empty_input_returns_zeroed_metrics()
    {
        var metrics = AdminAnalytics.Aggregate([]);

        Assert.Equal(0, metrics.TotalExecutions);
        Assert.Equal(0, metrics.SemanticCacheHits);
        Assert.Equal(0d, metrics.SemanticCacheHitRate);
        Assert.Equal(0, metrics.TotalLlmTokens);
        Assert.Equal(0, metrics.SensitiveAccessCount);
        Assert.Equal(0, metrics.OverrideCount);
        Assert.Equal(0, metrics.P95DurationMs);
        Assert.Empty(metrics.ExportCounts);
        Assert.Empty(metrics.DataSourceCounts);
        Assert.Null(metrics.LastExecutionAt);
    }

    [Fact]
    public void Computes_counts_rate_and_token_spend()
    {
        var docs = new[]
        {
            Doc("Mongo", cacheHit: true, tokens: 10, sensitive: true, over: false, duration: 10),
            Doc("Mongo", cacheHit: false, tokens: 20, sensitive: false, over: true, duration: 20),
            Doc("EnterpriseCoreREST", cacheHit: false, tokens: 30, sensitive: false, over: false, duration: 30),
            Doc("Mongo", cacheHit: false, tokens: 40, sensitive: false, over: false, duration: 40)
        };

        var metrics = AdminAnalytics.Aggregate(docs);

        Assert.Equal(4, metrics.TotalExecutions);
        Assert.Equal(1, metrics.SemanticCacheHits);
        Assert.Equal(0.25d, metrics.SemanticCacheHitRate, 5);
        Assert.Equal(100, metrics.TotalLlmTokens);
        Assert.Equal(1, metrics.SensitiveAccessCount);
        Assert.Equal(1, metrics.OverrideCount);
        Assert.Equal(3, metrics.DataSourceCounts["Mongo"]);
        Assert.Equal(1, metrics.DataSourceCounts["EnterpriseCoreREST"]);
    }

    [Fact]
    public void P95_uses_the_nearest_rank()
    {
        var docs = Enumerable.Range(1, 20)
            .Select(index => Doc("Mongo", false, 0, false, false, index))
            .ToList();

        var metrics = AdminAnalytics.Aggregate(docs);

        Assert.Equal(19, metrics.P95DurationMs);
    }

    [Fact]
    public void Groups_exports_and_uses_the_latest_timestamp()
    {
        var early = DateTimeOffset.Parse("2026-09-01T00:00:00Z");
        var late = DateTimeOffset.Parse("2026-09-30T12:00:00Z");
        var docs = new[]
        {
            Doc("Mongo", false, 0, false, false, 5, "CSV", early),
            Doc("Mongo", false, 0, false, false, 5, "CSV", early),
            Doc("Mongo", false, 0, false, false, 5, "XLSX", late),
            Doc("Mongo", false, 0, false, false, 5, null, early)
        };

        var metrics = AdminAnalytics.Aggregate(docs);

        Assert.Equal(2, metrics.ExportCounts["CSV"]);
        Assert.Equal(1, metrics.ExportCounts["XLSX"]);
        Assert.False(metrics.ExportCounts.ContainsKey("PDF"));
        Assert.Equal(late, metrics.LastExecutionAt);
    }

    private static AuditLogDocument Doc(
        string source,
        bool cacheHit,
        int tokens,
        bool sensitive,
        bool over,
        long duration,
        string? exportFormat = null,
        DateTimeOffset? timestamp = null)
    {
        return new AuditLogDocument(
            Guid.NewGuid().ToString("N"),
            timestamp ?? DateTimeOffset.UnixEpoch,
            "sess",
            source,
            new AuditLogUser("usr_1", "Bikash", "bnayak@enterprise.com", "Business Analyst"),
            new AuditLogNlpPerformance(cacheHit, false, tokens, duration),
            new AuditLogRequestDetails("prompt", new Dictionary<string, string>()),
            new AuditLogExecutionDetails("[{}]", "listingsAndReviews", 0, exportFormat, null),
            new AuditLogGovernance(sensitive, [], null, over, null));
    }
}
