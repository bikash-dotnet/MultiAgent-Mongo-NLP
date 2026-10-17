using Gateway.Auth;
using Gateway.Conversations;
using System.Security.Claims;

namespace Gateway.Handlers;

public static class ConversationHandlers
{
    public static async Task<IResult> PostStart(
        ConversationStartRequest request,
        ClaimsPrincipal user,
        ConversationOrchestrator conversations,
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
        var email = user.FindFirst(SessionClaims.Email)?.Value;
        var turn = await conversations.StartAsync(utterance, sessionId, email, cancellationToken, SessionClaims.ToRequesterContext(user));
        return Results.Ok(turn);
    }

    public static async Task<IResult> PostAnswer(
        string id,
        ConversationAnswer answer,
        ConversationOrchestrator conversations,
        CancellationToken cancellationToken)
    {
        var turn = await conversations.AnswerAsync(id, answer, cancellationToken);
        return turn is null ? Results.NotFound() : Results.Ok(turn);
    }

    public static async Task<IResult> Get(
        string id,
        ConversationOrchestrator conversations,
        CancellationToken cancellationToken)
    {
        var turn = await conversations.GetAsync(id, cancellationToken);
        return turn is null ? Results.NotFound() : Results.Ok(turn);
    }
}
