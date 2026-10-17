using Gateway.Auth;
using Gateway.Greeting;
using System.Security.Claims;

namespace Gateway.Handlers;

public static class SessionHandler
{
    public static IResult GetGreeting(ClaimsPrincipal user, TimeProvider clock)
    {
        var name = SessionClaims.DisplayName(user);
        var period = GreetingClock.ResolvePeriod(TimeOnly.FromTimeSpan(clock.GetLocalNow().TimeOfDay));
        var chips = new[]
        {
            "Listings in Los Angeles",
            "Pools under $200",
            "Just run it"
        };
        return Results.Ok(new GreetingResponse(name, period, GreetingClock.FormatMessage(name, period), chips));
    }
}
