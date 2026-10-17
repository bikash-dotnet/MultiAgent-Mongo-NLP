using Gateway.Analytics;
using Gateway.Audit;
using Gateway.Auth;
using System.Security.Claims;

namespace Gateway.Endpoints;

public static class AnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/analytics", async (IAuditLogStore audit, CancellationToken cancellationToken) =>
        {
            var documents = await audit.ListAsync(cancellationToken);
            return Microsoft.AspNetCore.Http.Results.Ok(AdminAnalytics.Aggregate(documents));
        }).RequireAuthorization();

        return app;
    }
}
