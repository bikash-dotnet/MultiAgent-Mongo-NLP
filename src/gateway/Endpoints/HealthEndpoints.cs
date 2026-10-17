using Gateway.Handlers;

namespace Gateway.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", HealthHandler.GetHealth);

        return app;
    }
}
