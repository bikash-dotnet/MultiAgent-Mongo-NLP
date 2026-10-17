using Gateway.Analytics;
using Gateway.Audit;
using Gateway.Auth;

namespace Gateway.Endpoints;

public static class AnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin")
            .RequireAuthorization();

        admin.MapGet("/analytics", async (IAuditLogStore audit, CancellationToken cancellationToken) =>
        {
            var documents = await audit.ListAsync(cancellationToken);
            return Microsoft.AspNetCore.Http.Results.Ok(AdminAnalytics.Aggregate(documents));
        });

        return app;
    }
}
