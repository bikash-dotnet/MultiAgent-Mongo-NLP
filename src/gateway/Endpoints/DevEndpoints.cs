using Gateway.Handlers;

namespace Gateway.Endpoints;

public static class DevEndpoints
{
    public static IEndpointRouteBuilder MapDevEndpoints(this IEndpointRouteBuilder app, string signingKey, string issuer, string audience)
    {
        var env = app.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        if (!env.IsDevelopment())
        {
            return app;
        }

        var dev = app.MapGroup("/dev");

        dev.MapGet("/token", () => DevHandlers.GetToken(
            app.ServiceProvider.GetRequiredService<IConfiguration>(),
            signingKey,
            issuer,
            audience));

        dev.MapPost("/enterprise-core/query", (EnterpriseCoreQueryRequest body) =>
            DevHandlers.PostEnterpriseCoreQuery(body));

        return app;
    }
}
