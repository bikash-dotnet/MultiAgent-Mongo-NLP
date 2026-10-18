namespace Gateway.Audit;

using Gateway.Schemas;

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
    AuditLogGovernance Governance,
    string? SchemaAction,
    int? SchemaVersion)
{
    public static AuditLogDocument ForSchemaManagement(
        string id,
        DateTimeOffset timestamp,
        string userId,
        SchemaDataSource dataSource,
        string action,
        int version,
        string? notes = null)
    {
        var viewer = new AuditLogUser(userId, userId, null, string.Empty);
        return new AuditLogDocument(
            id,
            timestamp,
            "schema-management",
            dataSource.ToString(),
            viewer,
            new AuditLogNlpPerformance(false, false, 0, 0),
            new AuditLogRequestDetails("", System.Collections.Immutable.ImmutableDictionary<string, string>.Empty),
            new AuditLogExecutionDetails("", "schemas", 0, null, null),
            new AuditLogGovernance(false, [], null, false, $"{viewer.Name}: {action}" + (notes is not null ? $" ({notes})" : string.Empty)),
            action,
            version);
    }
}
