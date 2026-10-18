using Gateway.Handlers;
using Gateway.Rules;
using Gateway.Infrastructure.Filters;

namespace Gateway.Endpoints;

public static class ColumnDefinitionEndpoints
{
    public static IEndpointRouteBuilder MapColumnDefinitionEndpoints(this IEndpointRouteBuilder app)
    {
        var readOnly = app.MapGroup("/api/column-definitions")
            .RequireAuthorization();

        readOnly.MapGet("", ColumnDefinitionHandlers.ListDefinitions);
        readOnly.MapGet("/{id}", ColumnDefinitionHandlers.GetDefinition);

        var mutating = app.MapGroup("/api/column-definitions")
            .RequireAuthorization()
            .AddEndpointFilter<DataOwnerOrAdminFilter>();

        mutating.MapPost("", ColumnDefinitionHandlers.CreateDefinition);
        mutating.MapPut("/{id}", ColumnDefinitionHandlers.UpdateDefinition);
        mutating.MapDelete("/{id}", ColumnDefinitionHandlers.DeleteDefinition);
        mutating.MapPost("/reorder", ColumnDefinitionHandlers.ReorderDefinitions);

        return app;
    }
}
