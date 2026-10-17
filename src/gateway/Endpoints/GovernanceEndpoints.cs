using Gateway.Handlers;

namespace Gateway.Endpoints;

public static class GovernanceEndpoints
{
    public static IEndpointRouteBuilder MapGovernanceEndpoints(this IEndpointRouteBuilder app)
    {
        var governance = app.MapGroup("/api/governance")
            .RequireAuthorization();

        governance.MapGet("/approval", GovernanceHandlers.GetApproval);

        governance.MapPut("/approval", GovernanceHandlers.PutApproval);

        return app;
    }
}
