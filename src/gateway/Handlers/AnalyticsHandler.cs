using Gateway.Analytics;
using Gateway.Audit;

namespace Gateway.Handlers;

public static class AnalyticsHandler
{
    public static async Task<IResult> GetAnalytics(IAuditLogStore audit, CancellationToken cancellationToken)
    {
        var documents = await audit.ListAsync(cancellationToken);
        return Results.Ok(AdminAnalytics.Aggregate(documents));
    }
}
