using Gateway.Auth;
using Gateway.Configuration;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Gateway.Handlers;

public static class ConfigHandlers
{
    public static async Task<IResult> ListConfig(
        ConfigService service,
        CancellationToken ct)
    {
        var sections = await service.GetAllAsync(ct);
        return Results.Ok(sections);
    }

    public static async Task<IResult> GetConfigSection(
        string section,
        ConfigService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await service.GetSectionAsync(section, user, ct);
            return Results.Ok(snapshot);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound(new { error = $"Configuration section '{section}' not found." });
        }
    }

    public static IResult GetConfigSchema(ConfigService service)
    {
        return Results.Ok(service.GetSchema());
    }

    public static async Task<IResult> UpdateConfigSection(
        string section,
        UpdateConfigSectionRequest request,
        ClaimsPrincipal user,
        ConfigService service,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await service.UpdateSectionAsync(section, request, user, ct);
            return Results.Ok(snapshot);
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

    public static async Task<IResult> DeleteConfigSection(
        string section,
        ClaimsPrincipal user,
        ConfigService service,
        CancellationToken ct)
    {
        try
        {
            await service.DeleteSectionAsync(section, user, ct);
            return Results.NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
    }

    public static async Task<IResult> ResetConfig(
        ConfigResetRequest request,
        ConfigService service,
        CancellationToken ct)
    {
        if (!request.Confirm)
        {
            return Results.BadRequest(new { error = "Reset requires confirm=true." });
        }

        await service.ResetAllAsync(ct);
        return Results.NoContent();
    }
}
