using System.Text.Json;
using Gateway.Execution;
using Gateway.Governance;
using Gateway.Nlp.Guardrails;
using Gateway.Nlp.Llm;
using Gateway.Nlp.Mql;
using Gateway.Nlp.Router;
using Gateway.Nlp.Slots;
using Gateway.Rules;
using Microsoft.Extensions.Options;

namespace Gateway.Nlp.Orchestrator;

public sealed class NlpOrchestrator : INlpOrchestrator
{
    private readonly INlpRouter _router;
    private readonly Gazetteer _gazetteer;
    private readonly SelfCorrectingLlmQueryGenerator _generator;
    private readonly IAgentEventSink _events;
    private readonly IAgentStateStore _stateStore;
    private readonly GuardrailEvaluator _guardrails;
    private readonly IAccessRequestStore _accessRequests;
    private readonly TimeProvider _clock;
    private readonly ITabularQueryExecutor? _executor;
    private readonly ExecutionOptions _execution;
    private readonly BusinessRuleEngine _rules;

    public NlpOrchestrator(
        INlpRouter router,
        Gazetteer gazetteer,
        SelfCorrectingLlmQueryGenerator generator,
        IAgentEventSink events,
        IAgentStateStore stateStore,
        GuardrailEvaluator guardrails,
        IAccessRequestStore accessRequests,
        TimeProvider clock,
        BusinessRuleEngine rules,
        ITabularQueryExecutor? executor = null,
        IOptions<ExecutionOptions>? executionOptions = null)
    {
        _router = router;
        _gazetteer = gazetteer;
        _generator = generator;
        _events = events;
        _stateStore = stateStore;
        _guardrails = guardrails;
        _accessRequests = accessRequests;
        _clock = clock;
        _rules = rules;
        _executor = executor;
        _execution = executionOptions?.Value ?? new ExecutionOptions();
    }

    public async Task<NlpRouteResult> OrchestrateAsync(
        string utterance,
        string sessionId = "anonymous",
        CancellationToken cancellationToken = default,
        RequesterContext? requester = null)
    {
        _events.Publish(new AgentEvent("agent.started", "started", utterance));

        var routed = _router.Route(utterance);

        if (routed.Kind == NlpRouteKind.ClarifyRequired)
        {
            _events.Publish(new AgentEvent("agent.clarifying", "clarifying", routed.Question));
            await _stateStore.SaveAsync(
                new AgentState(sessionId, "clarifying", routed.Question, _clock.GetUtcNow()),
                cancellationToken);
            return routed;
        }

        var result = routed;

        if (routed.Kind == NlpRouteKind.ComplexLlmRequired)
        {
            var slots = SlotExtractor.Extract(utterance, _gazetteer);
            var slotsJson = JsonSerializer.Serialize(slots);

            SelfCorrectionResult corrected;
            try
            {
                corrected = await _generator.GenerateAsync(utterance, slotsJson, cancellationToken);
            }
            catch (Exception ex)
            {
                _events.Publish(new AgentEvent("agent.completed", "failed", ex.Message));
                return routed with
                {
                    Kind = NlpRouteKind.ComplexLlmFailed,
                    LlmAttempts = 1,
                    Error = ex.Message
                };
            }

            if (corrected.Pipeline is null)
            {
                _events.Publish(new AgentEvent("agent.completed", "failed", corrected.Error));
                return routed with
                {
                    Kind = NlpRouteKind.ComplexLlmFailed,
                    LlmAttempts = corrected.Attempts,
                    LlmTokensConsumed = corrected.TokensConsumed,
                    Error = corrected.Error
                };
            }

            result = routed with
            {
                Mql = corrected.Pipeline,
                LlmAttempts = corrected.Attempts,
                LlmTokensConsumed = corrected.TokensConsumed
            };
        }

        if (result.Mql is null)
        {
            _events.Publish(new AgentEvent("agent.completed", "completed", result.Kind.ToString()));
            return result;
        }

        var guard = await _guardrails.EvaluateAsync(
            result.Mql,
            requester?.Role,
            schemaId: null,
            requester,
            cancellationToken);

        if (guard.Outcome == GuardrailOutcome.Rejected)
        {
            _events.Publish(new AgentEvent("agent.completed", "failed", guard.Reason));
            return result with
            {
                Kind = NlpRouteKind.Rejected,
                GuardrailReason = guard.Reason,
                Error = guard.Reason
            };
        }

        if (guard.Outcome == GuardrailOutcome.PausedForApproval)
        {
            var request = await _accessRequests.CreateAsync(
                new AccessRequest(
                    string.Empty,
                    sessionId,
                    result.Mql,
                    guard.SensitiveFields,
                    AccessRequest.PendingLead,
                    Justification: null,
                    _clock.GetUtcNow(),
                    Requester: requester is null
                        ? null
                        : new RequesterInfo(requester.UserId, requester.Name, requester.Role, requester.LeadUserId),
                    AssignedLeadId: requester?.LeadUserId,
                    RequestedFlags: guard.SensitiveFields
                        .Select(field => new RequestedFlag(field, "requires_approval"))
                        .ToList()),
                cancellationToken);

            _events.Publish(new AgentEvent("governance.paused", "paused", guard.Reason));
            await _stateStore.SaveAsync(
                new AgentState(sessionId, "governance_paused", guard.Reason, _clock.GetUtcNow(), request.Id),
                cancellationToken);

            return result with
            {
                Kind = NlpRouteKind.GovernancePaused,
                SensitiveFields = guard.SensitiveFields,
                AccessRequestId = request.Id,
                GuardrailReason = guard.Reason
            };
        }

        if (guard.ExemptionType is not null)
        {
            _events.Publish(new AgentEvent("governance.exempted", "exempted", guard.ExemptionType));
        }

        var sensitiveFields = guard.SensitiveFields.Count > 0 ? guard.SensitiveFields : null;

        if (_executor is null || result.Mql is null)
        {
            _events.Publish(new AgentEvent("agent.completed", "completed", result.Kind.ToString()));
            return result with { SensitiveFields = sensitiveFields };
        }

        var governance = new GovernanceDecision(
            guard.SensitiveFields.Count > 0,
            guard.SensitiveFields,
            guard.ExemptionType,
            false,
            guard.ExemptionType is not null ? requester?.Name : null);

        var columns = MqlAnalyzer.Analyze(result.Mql).FieldPaths;

        // Apply business rule setDefault values before query execution parameters are finalized.
        var ruleResult = await _rules.EvaluateAsync(
            requester ?? new RequesterContext(sessionId, sessionId, string.Empty, null),
            schemaId: null,
            cancellationToken);
        var setDefaults = BusinessRuleEngine.SetDefaults(ruleResult.Effects);
        var defaults = result.ClarificationsApplied;
        var effectiveDefaults = new MqlDefaults(
            setDefaults.TryGetValue("limit", out var limit) && int.TryParse(limit, out var l) ? l : defaults.Limit,
            setDefaults.TryGetValue("sort", out var sort) ? sort : defaults.Sort,
            setDefaults.TryGetValue("market", out var market) ? market : defaults.Market);

        var user = requester ?? new RequesterContext(sessionId, sessionId, string.Empty, null);
        var execution = await _executor.ExecuteAsync(
            new ExecutionRequest(
                result.Mql,
                sessionId,
                utterance,
                _execution.DataSource,
                _execution.Collection,
                columns,
                new Dictionary<string, string>
                {
                    ["limit"] = effectiveDefaults.Limit.ToString(),
                    ["sort"] = effectiveDefaults.Sort,
                    ["market"] = effectiveDefaults.Market
                },
                result.SemanticCacheHit,
                result.SlotExtractionUsed,
                result.LlmTokensConsumed,
                user,
                governance),
            cancellationToken);

        return result with
        {
            SensitiveFields = sensitiveFields,
            Columns = execution.Columns,
            Rows = execution.Rows,
            DataSource = execution.DataSource,
            RowCount = execution.RowCount,
            DurationMs = execution.DurationMs,
            ExecutionError = execution.Error
        };
    }
}
