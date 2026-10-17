using Gateway.Auth;
using Gateway.Infrastructure.Filters;
using Gateway.Schemas;
using Gateway.Services;
using Gateway.Handlers;

namespace Gateway.Endpoints;

public static class SchemaEndpoints
{
    public static IEndpointRouteBuilder MapSchemaEndpoints(this IEndpointRouteBuilder app)
    {
        var readOnly = app.MapGroup("/api/schemas")
            .RequireAuthorization();

        readOnly.MapGet("", SchemaHandlers.ListSchemas);
        readOnly.MapGet("/{id}", SchemaHandlers.GetSchemaById);

        var mutating = app.MapGroup("/api/schemas")
            .RequireAuthorization()
            .AddEndpointFilter<DataOwnerOrAdminFilter>();

        mutating.MapPost("", SchemaHandlers.CreateSchema);
        mutating.MapPut("/{id}", SchemaHandlers.UpdateSchema);
        mutating.MapPost("/{id}/fields", SchemaHandlers.AddField);
        mutating.MapPut("/{id}/fields/{path}", SchemaHandlers.UpdateField);
        mutating.MapDelete("/{id}/fields/{path}", SchemaHandlers.DeleteField);
        mutating.MapPost("/{id}/activate", SchemaHandlers.ActivateSchema);
        mutating.MapPost("/import", SchemaHandlers.ImportSchema);
        mutating.MapDelete("/{id}", SchemaHandlers.DeleteSchema);

        return app;
    }
}
