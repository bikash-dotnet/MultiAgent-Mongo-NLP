using Gateway.Handlers;

namespace Gateway.Endpoints;

public static class AnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin")
            .RequireAuthorization();

        admin.MapGet("/analytics", AnalyticsHandler.GetAnalytics);

        return app;
    }
}
