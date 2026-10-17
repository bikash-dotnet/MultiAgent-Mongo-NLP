using Gateway.Auth;
using Gateway.Nlp.Http;
using Gateway.Nlp.Orchestrator;
using System.Security.Claims;

namespace Gateway.Endpoints;

public static class NlpEndpoints
{
    public static IEndpointRouteBuilder MapNlpEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/nlp/query", async (
            NlpQueryRequest request,
            ClaimsPrincipal user,
            INlpOrchestrator orchestrator,
            CancellationToken cancellationToken) =>
        {
            var utterance = request.Utterance?.Trim();
            if (string.IsNullOrWhiteSpace(utterance))
            {
                return Microsoft.AspNetCore.Http.Results.BadRequest(new { error = "utterance is required" });
            }

            if (utterance.Length > 500)
            {
                return Microsoft.AspNetCore.Http.Results.BadRequest(new { error = "utterance must be 500 characters or fewer" });
            }

            var sessionId = user.FindFirst(SessionClaims.UserId)?.Value ?? "anonymous";
            var result = await orchestrator.OrchestrateAsync(utterance, sessionId, cancellationToken, SessionClaims.ToRequesterContext(user));
            return Microsoft.AspNetCore.Http.Results.Ok(NlpQueryResponse.From(result));
        }).RequireAuthorization();

        return app;
    }
}
