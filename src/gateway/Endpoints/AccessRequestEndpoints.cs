using Gateway.Handlers;

namespace Gateway.Endpoints;

public static class AccessRequestEndpoints
{
    public static IEndpointRouteBuilder MapAccessRequestEndpoints(this IEndpointRouteBuilder app)
    {
        var requests = app.MapGroup("/api/access-requests")
            .RequireAuthorization();

        requests.MapGet("", AccessRequestHandlers.List);

        requests.MapGet("/{id}", AccessRequestHandlers.Get);

        requests.MapPost("/{id}/approve", AccessRequestHandlers.Approve);

        requests.MapPost("/{id}/reject", AccessRequestHandlers.Reject);

        requests.MapPost("/{id}/override", AccessRequestHandlers.Override);

        return app;
    }
}
