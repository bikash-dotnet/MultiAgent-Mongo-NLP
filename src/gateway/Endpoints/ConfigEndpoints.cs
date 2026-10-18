using Gateway.Configuration;
using Gateway.Handlers;
using Gateway.Infrastructure.Filters;

namespace Gateway.Endpoints;

public static class ConfigEndpoints
{
    public static IEndpointRouteBuilder MapConfigEndpoints(this IEndpointRouteBuilder app)
    {
        var readOnly = app.MapGroup("/api/config")
            .RequireAuthorization();

        readOnly.MapGet("", ConfigHandlers.ListConfig);
        readOnly.MapGet("/{section}", ConfigHandlers.GetConfigSection);
        readOnly.MapGet("/schema", ConfigHandlers.GetConfigSchema);

        var mutating = app.MapGroup("/api/config")
            .RequireAuthorization()
            .AddEndpointFilter<DataOwnerOrAdminFilter>();

        mutating.MapPut("/{section}", ConfigHandlers.UpdateConfigSection);
        mutating.MapDelete("/{section}", ConfigHandlers.DeleteConfigSection);
        mutating.MapPost("/reset", ConfigHandlers.ResetConfig);

        return app;
    }
}
