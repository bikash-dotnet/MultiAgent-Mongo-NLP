using Gateway.Nlp.Router;

namespace Gateway.Nlp.Orchestrator;

public interface INlpOrchestrator
{
    Task<NlpRouteResult> OrchestrateAsync(string utterance, string sessionId = "anonymous", CancellationToken cancellationToken = default);
}
