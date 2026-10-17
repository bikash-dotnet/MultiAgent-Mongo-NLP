using Gateway.Handlers;
using Gateway.Hubs;

namespace Gateway.Endpoints;

public static class AgentEndpoints
{
    public static IEndpointRouteBuilder MapAgentEndpoints(this IEndpointRouteBuilder app)
    {
        var agents = app.MapGroup("/api/agents")
            .RequireAuthorization();

        agents.MapGet("/stream", AgentHandler.Stream);

        app.MapHub<AgentHub>("/hubs/agent-stream").RequireAuthorization();

        return app;
    }
}
