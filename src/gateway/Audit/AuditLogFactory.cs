using Gateway.Execution;

namespace Gateway.Audit;

public static class AuditLogFactory
{
    public static AuditLogDocument Build(string id, DateTimeOffset timestamp, ExecutionRequest request, TabularResult result)
    {
        return new AuditLogDocument(
            id,
            timestamp,
            request.SessionId,
            result.DataSource,
            new AuditLogUser(request.User.UserId, request.User.Name, request.User.Email, request.User.Role),
            new AuditLogNlpPerformance(
                request.SemanticCacheHit,
                request.SlotExtractionUsed,
                request.LlmTokensConsumed,
                result.DurationMs),
            new AuditLogRequestDetails(request.Utterance, request.ClarificationsApplied),
            new AuditLogExecutionDetails(
                request.Mql,
                request.TargetCollection,
                result.RowCount,
                request.ExportFormat,
                result.Error),
            new AuditLogGovernance(
                request.Governance.SensitiveDataAccessed,
                request.Governance.FlagsTriggered,
                request.Governance.ExemptionType,
                request.Governance.OverrideInvoked,
                request.Governance.AuthorizedBy));
    }
}
