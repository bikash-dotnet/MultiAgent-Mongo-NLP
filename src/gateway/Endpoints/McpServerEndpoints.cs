using Gateway.Handlers;
using Gateway.Infrastructure.Filters;

namespace Gateway.Endpoints;

public static class McpServerEndpoints
{
    public static IEndpointRouteBuilder MapMcpServerEndpoints(this IEndpointRouteBuilder app)
    {
        var readOnly = app.MapGroup("/api/mcp-servers")
            .RequireAuthorization();

        readOnly.MapGet("", McpServerHandlers.List);
        readOnly.MapGet("/{id}", McpServerHandlers.Get);
        readOnly.MapGet("/{id}/tools", McpServerHandlers.Tools);

        var mutating = app.MapGroup("/api/mcp-servers")
            .RequireAuthorization()
            .AddEndpointFilter<DataOwnerOrAdminFilter>();

        mutating.MapPost("", McpServerHandlers.Create);
        mutating.MapPut("/{id}", McpServerHandlers.Update);
        mutating.MapDelete("/{id}", McpServerHandlers.Delete);
        mutating.MapPost("/{id}/enable", McpServerHandlers.Enable);
        mutating.MapPost("/{id}/disable", McpServerHandlers.Disable);
        mutating.MapPost("/{id}/test", McpServerHandlers.Test);

        return app;
    }
}
