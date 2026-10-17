using Gateway.Infrastructure.Results;

namespace Gateway.Handlers;

public static class HealthHandler
{
    public static IResult GetHealth() =>
        ApiResult<object>.Ok(new { status = "ok" }).ToResult();
}
