using Gateway.Governance;

namespace Gateway.Execution;

public sealed record ExecutionRequest(
    string Mql,
    string SessionId,
    string Utterance,
    ExecutionDataSource DataSource,
    string TargetCollection,
    IReadOnlyList<string> Columns,
    IReadOnlyDictionary<string, string> ClarificationsApplied,
    bool SemanticCacheHit,
    bool SlotExtractionUsed,
    int LlmTokensConsumed,
    RequesterContext User,
    GovernanceDecision Governance,
    string? ExportFormat = null);
