using Gateway.Auth;
using Gateway.Greeting;
using System.Security.Claims;

namespace Gateway.Endpoints;

public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/session/greeting", (ClaimsPrincipal user, TimeProvider clock) =>
        {
            var name = SessionClaims.DisplayName(user);
            var period = GreetingClock.ResolvePeriod(TimeOnly.FromTimeSpan(clock.GetLocalNow().TimeOfDay));
            var chips = new[]
            {
                "Listings in Los Angeles",
                "Pools under $200",
                "Just run it"
            };
            return Microsoft.AspNetCore.Http.Results.Ok(new GreetingResponse(name, period, GreetingClock.FormatMessage(name, period), chips));
        }).RequireAuthorization();

        return app;
    }
}
