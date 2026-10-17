using Gateway.Auth;
using Gateway.Hubs;
using Gateway.Nlp.Orchestrator;
using Microsoft.AspNetCore.SignalR;
namespace Gateway.Endpoints;

public static class AgentEndpoints
{
    public static IEndpointRouteBuilder MapAgentEndpoints(this IEndpointRouteBuilder app)
    {
        var agents = app.MapGroup("/api/agents")
            .RequireAuthorization();

        agents.MapGet("/stream", async (HttpContext context, IAgentEventSink events, CancellationToken cancellationToken) =>
        {
            context.Response.Headers.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache, no-transform";
            context.Response.Headers.Connection = "keep-alive";
            context.Response.Headers["X-Accel-Buffering"] = "no";
            context.Response.Headers["Pragma"] = "no-cache";

            await WriteAgentEvent(context, new AgentEvent("agent.idle", "idle"), cancellationToken);

            try
            {
                await foreach (var agentEvent in events.ReadAllAsync(cancellationToken))
                {
                    await WriteAgentEvent(context, agentEvent, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        app.MapHub<AgentHub>("/hubs/agent-stream").RequireAuthorization();

        return app;
    }

    static async Task WriteAgentEvent(HttpContext context, AgentEvent agentEvent, CancellationToken cancellationToken)
    {
        var payload = System.Text.Json.JsonSerializer.Serialize(new { status = agentEvent.Status, detail = agentEvent.Detail });
        await context.Response.WriteAsync($"event: {agentEvent.Name}\n", cancellationToken);
        await context.Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
        await context.Response.Body.FlushAsync(cancellationToken);
    }
}
