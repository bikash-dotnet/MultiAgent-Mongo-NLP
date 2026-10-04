export interface AdminAnalyticsMetrics {
  totalExecutions: number;
  semanticCacheHits: number;
  semanticCacheHitRate: number;
  totalLlmTokens: number;
  sensitiveAccessCount: number;
  overrideCount: number;
  p95DurationMs: number;
  exportCounts: Record<string, number>;
  dataSourceCounts: Record<string, number>;
  lastExecutionAt: string | null;
}
