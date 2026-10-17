using Gateway.Infrastructure.Results;

namespace Gateway.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () =>
        {
            var result = ApiResult<object>.Ok(new { status = "ok" });
            return result.ToResult();
        });

        return app;
    }
}
