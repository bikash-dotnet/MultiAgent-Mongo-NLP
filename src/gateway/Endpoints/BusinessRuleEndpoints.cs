using Gateway.Handlers;
using Gateway.Infrastructure.Filters;
using Gateway.Rules;

namespace Gateway.Endpoints;

public static class BusinessRuleEndpoints
{
    public static IEndpointRouteBuilder MapBusinessRuleEndpoints(this IEndpointRouteBuilder app)
    {
        var readOnly = app.MapGroup("/api/business-rules")
            .RequireAuthorization();

        readOnly.MapGet("", BusinessRuleHandlers.ListRules);
        readOnly.MapGet("/{id}", BusinessRuleHandlers.GetRule);

        var mutating = app.MapGroup("/api/business-rules")
            .RequireAuthorization()
            .AddEndpointFilter<DataOwnerOrAdminFilter>();

        mutating.MapPost("", BusinessRuleHandlers.CreateRule);
        mutating.MapPut("/{id}", BusinessRuleHandlers.UpdateRule);
        mutating.MapDelete("/{id}", BusinessRuleHandlers.DeleteRule);

        return app;
    }
}
