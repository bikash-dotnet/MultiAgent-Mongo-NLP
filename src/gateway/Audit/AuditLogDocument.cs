namespace Gateway.Audit;

public sealed record AuditLogUser(string UserId, string Name, string? Email, string Role);

public sealed record AuditLogNlpPerformance(bool SemanticCacheHit, bool SlotExtractionUsed, int LlmTokensConsumed, long ExecutionDurationMs);

public sealed record AuditLogRequestDetails(string NaturalLanguagePrompt, IReadOnlyDictionary<string, string> ClarificationsApplied);

public sealed record AuditLogExecutionDetails(string GeneratedQuery, string TargetCollection, int RowsReturned, string? ExportFormat, string? Error);

public sealed record AuditLogGovernance(bool SensitiveDataAccessed, IReadOnlyList<string> FlagsTriggered, string? ExemptionType, bool OverrideInvoked, string? AuthorizedBy);

public sealed record AuditLogDocument(
    string Id,
    DateTimeOffset AuditTimestamp,
    string SessionId,
    string DataSource,
    AuditLogUser User,
    AuditLogNlpPerformance NlpPerformance,
    AuditLogRequestDetails RequestDetails,
    AuditLogExecutionDetails ExecutionDetails,
    AuditLogGovernance Governance);
