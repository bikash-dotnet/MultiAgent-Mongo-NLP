using System.Text.Json;
using Gateway.Nlp.Guardrails;
using Gateway.Nlp.Llm;
using Gateway.Nlp.Router;
using Gateway.Nlp.Slots;

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

    public NlpOrchestrator(
        INlpRouter router,
        Gazetteer gazetteer,
        SelfCorrectingLlmQueryGenerator generator,
        IAgentEventSink events,
        IAgentStateStore stateStore,
        GuardrailEvaluator guardrails,
        IAccessRequestStore accessRequests,
        TimeProvider clock)
    {
        _router = router;
        _gazetteer = gazetteer;
        _generator = generator;
        _events = events;
        _stateStore = stateStore;
        _guardrails = guardrails;
        _accessRequests = accessRequests;
        _clock = clock;
    }

    public async Task<NlpRouteResult> OrchestrateAsync(
        string utterance,
        string sessionId = "anonymous",
        CancellationToken cancellationToken = default)
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

        var guard = _guardrails.Evaluate(result.Mql);

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
                    _clock.GetUtcNow()),
                cancellationToken);

            _events.Publish(new AgentEvent("governance.paused", "paused", guard.Reason));
            await _stateStore.SaveAsync(
                new AgentState(sessionId, "governance_paused", guard.Reason, _clock.GetUtcNow()),
                cancellationToken);

            return result with
            {
                Kind = NlpRouteKind.GovernancePaused,
                SensitiveFields = guard.SensitiveFields,
                AccessRequestId = request.Id,
                GuardrailReason = guard.Reason
            };
        }

        _events.Publish(new AgentEvent("agent.completed", "completed", result.Kind.ToString()));
        return result with
        {
            SensitiveFields = guard.SensitiveFields.Count > 0 ? guard.SensitiveFields : null
        };
    }
}
