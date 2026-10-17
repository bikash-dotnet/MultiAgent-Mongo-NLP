using Gateway.Auth;
using Gateway.Conversations;
using System.Security.Claims;

namespace Gateway.Endpoints;

public static class ConversationEndpoints
{
    public static IEndpointRouteBuilder MapConversationEndpoints(this IEndpointRouteBuilder app)
    {
        var conversations = app.MapGroup("/api/conversations")
            .RequireAuthorization();

        conversations.MapPost("", async (
            ConversationStartRequest request,
            ClaimsPrincipal user,
            ConversationOrchestrator conversations,
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
            var email = user.FindFirst(SessionClaims.Email)?.Value;
            var turn = await conversations.StartAsync(utterance, sessionId, email, cancellationToken, SessionClaims.ToRequesterContext(user));
            return Microsoft.AspNetCore.Http.Results.Ok(turn);
        });

        conversations.MapPost("/{id}/answers", async (
            string id,
            ConversationAnswer answer,
            ConversationOrchestrator conversations,
            CancellationToken cancellationToken) =>
        {
            var turn = await conversations.AnswerAsync(id, answer, cancellationToken);
            return turn is null ? Microsoft.AspNetCore.Http.Results.NotFound() : Microsoft.AspNetCore.Http.Results.Ok(turn);
        });

        conversations.MapGet("/{id}", async (
            string id,
            ConversationOrchestrator conversations,
            CancellationToken cancellationToken) =>
        {
            var turn = await conversations.GetAsync(id, cancellationToken);
            return turn is null ? Microsoft.AspNetCore.Http.Results.NotFound() : Microsoft.AspNetCore.Http.Results.Ok(turn);
        });

        return app;
    }
}
