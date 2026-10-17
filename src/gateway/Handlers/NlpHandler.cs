using Gateway.Auth;
using Gateway.Nlp.Http;
using Gateway.Nlp.Orchestrator;
using System.Security.Claims;

namespace Gateway.Handlers;

public static class NlpHandler
{
    public static async Task<IResult> PostQuery(
        NlpQueryRequest request,
        ClaimsPrincipal user,
        INlpOrchestrator orchestrator,
        CancellationToken cancellationToken)
    {
        var utterance = request.Utterance?.Trim();
        if (string.IsNullOrWhiteSpace(utterance))
        {
            return Results.BadRequest(new { error = "utterance is required" });
        }

        if (utterance.Length > 500)
        {
            return Results.BadRequest(new { error = "utterance must be 500 characters or fewer" });
        }

        var sessionId = user.FindFirst(SessionClaims.UserId)?.Value ?? "anonymous";
        var result = await orchestrator.OrchestrateAsync(utterance, sessionId, cancellationToken, SessionClaims.ToRequesterContext(user));
        return Results.Ok(NlpQueryResponse.From(result));
    }
}
