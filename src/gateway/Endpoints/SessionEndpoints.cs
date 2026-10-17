using Gateway.Handlers;

namespace Gateway.Endpoints;

public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var session = app.MapGroup("/api/session")
            .RequireAuthorization();

        session.MapGet("/greeting", SessionHandler.GetGreeting);

        return app;
    }
}
