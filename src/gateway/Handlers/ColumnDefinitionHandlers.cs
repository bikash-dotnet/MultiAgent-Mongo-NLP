using Gateway.Auth;
using Gateway.Rules;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Claims;

namespace Gateway.Handlers;

public static class ColumnDefinitionHandlers
{
    public static async Task<IResult> ListDefinitions(
        ColumnDefinitionService service,
        CancellationToken ct)
    {
        var definitions = await service.GetDefinitionsAsync(ct);
        return Results.Ok(definitions);
    }

    public static async Task<IResult> GetDefinition(
        string id,
        ColumnDefinitionService service,
        CancellationToken ct)
    {
        // Get all and find by id (store GetAsync by id is available)
        var all = await service.GetDefinitionsAsync(ct);
        var definition = all.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.Ordinal));
        if (definition is null)
        {
            return Results.NotFound(new { error = $"Column definition '{id}' not found." });
        }
        return Results.Ok(definition);
    }

    public static async Task<IResult> CreateDefinition(
        ColumnDefinitionRequest request,
        ClaimsPrincipal user,
        ColumnDefinitionService service,
        CancellationToken ct)
    {
        try
        {
            var userId = user.FindFirst(SessionClaims.UserId)?.Value ?? "anonymous";
            var definition = await service.CreateAsync(request, userId, ct);
            return Results.Ok(definition);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    public static async Task<IResult> UpdateDefinition(
        string id,
        ColumnDefinitionRequest request,
        ClaimsPrincipal user,
        ColumnDefinitionService service,
        CancellationToken ct,
        [FromQuery] int version)
    {
        try
        {
            var userId = user.FindFirst(SessionClaims.UserId)?.Value ?? "anonymous";
            var definition = await service.UpdateAsync(id, request, userId, version, ct);
            return Results.Ok(definition);
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    public static async Task<IResult> DeleteDefinition(
        string id,
        ColumnDefinitionService service,
        CancellationToken ct)
    {
        var deleted = await service.DeleteAsync(id, ct);
        if (!deleted)
        {
            return Results.NotFound(new { error = $"Column definition '{id}' not found." });
        }
        return Results.NoContent();
    }

    public static async Task<IResult> ReorderDefinitions(
        ColumnDefinitionReorderRequest request,
        ClaimsPrincipal user,
        ColumnDefinitionService service,
        CancellationToken ct)
    {
        var userId = user.FindFirst(SessionClaims.UserId)?.Value ?? "anonymous";
        await service.ReorderAsync(request, userId, ct);
        return Results.NoContent();
    }
}
