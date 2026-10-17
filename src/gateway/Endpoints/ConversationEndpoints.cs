using Gateway.Handlers;

namespace Gateway.Endpoints;

public static class ConversationEndpoints
{
    public static IEndpointRouteBuilder MapConversationEndpoints(this IEndpointRouteBuilder app)
    {
        var conversations = app.MapGroup("/api/conversations")
            .RequireAuthorization();

        conversations.MapPost("", ConversationHandlers.PostStart);

        conversations.MapPost("/{id}/answers", ConversationHandlers.PostAnswer);

        conversations.MapGet("/{id}", ConversationHandlers.Get);

        return app;
    }
}
