using Gateway.Handlers;

namespace Gateway.Endpoints;

public static class NlpEndpoints
{
    public static IEndpointRouteBuilder MapNlpEndpoints(this IEndpointRouteBuilder app)
    {
        var nlp = app.MapGroup("/api/nlp")
            .RequireAuthorization();

        nlp.MapPost("/query", NlpHandler.PostQuery);

        return app;
    }
}
