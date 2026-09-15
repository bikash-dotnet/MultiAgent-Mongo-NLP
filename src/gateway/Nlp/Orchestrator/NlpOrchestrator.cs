using System.Text.Json;
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

    public NlpOrchestrator(
        INlpRouter router,
        Gazetteer gazetteer,
        SelfCorrectingLlmQueryGenerator generator,
        IAgentEventSink events,
        IAgentStateStore stateStore)
    {
        _router = router;
        _gazetteer = gazetteer;
        _generator = generator;
        _events = events;
        _stateStore = stateStore;
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
                new AgentState(sessionId, "clarifying", routed.Question, DateTimeOffset.UtcNow),
                cancellationToken);
            return routed;
        }

        if (routed.Kind != NlpRouteKind.ComplexLlmRequired)
        {
            _events.Publish(new AgentEvent("agent.completed", "completed", routed.Kind.ToString()));
            return routed;
        }

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

        _events.Publish(new AgentEvent("agent.completed", "completed", "ComplexLlmRequired"));
        return routed with
        {
            Mql = corrected.Pipeline,
            LlmAttempts = corrected.Attempts,
            LlmTokensConsumed = corrected.TokensConsumed
        };
    }
}
