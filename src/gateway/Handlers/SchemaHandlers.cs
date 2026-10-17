using Gateway.Auth;
using Gateway.Schemas;
using Gateway.Services;
using System.Security.Claims;

namespace Gateway.Handlers;

public static class SchemaHandlers
{
    public static async Task<IResult> ListSchemas(ISchemaService service, CancellationToken ct) =>
        (await service.ListAsync(ct)).ToResult();

    public static async Task<IResult> GetSchemaById(string id, ISchemaService service, CancellationToken ct) =>
        (await service.GetByIdAsync(id, ct)).ToResult();

    public static async Task<IResult> CreateSchema(
        CreateSchemaRequest request,
        ClaimsPrincipal user,
        ISchemaService service,
        CancellationToken ct)
    {
        var userId = user.FindFirst(SessionClaims.UserId)?.Value ?? "anonymous";
        return (await service.CreateAsync(request, userId, ct)).ToResult();
    }

    public static async Task<IResult> UpdateSchema(
        string id,
        UpdateSchemaRequest request,
        ClaimsPrincipal user,
        ISchemaService service,
        CancellationToken ct)
    {
        var userId = user.FindFirst(SessionClaims.UserId)?.Value ?? "anonymous";
        return (await service.UpdateAsync(id, request, userId, ct)).ToResult();
    }

    public static async Task<IResult> AddField(
        string id,
        SchemaFieldRequest request,
        ClaimsPrincipal user,
        ISchemaService service,
        CancellationToken ct)
    {
        var userId = user.FindFirst(SessionClaims.UserId)?.Value ?? "anonymous";
        return (await service.AddFieldAsync(id, request, userId, ct)).ToResult();
    }

    public static async Task<IResult> UpdateField(
        string id,
        string path,
        SchemaFieldRequest request,
        ClaimsPrincipal user,
        ISchemaService service,
        CancellationToken ct)
    {
        var userId = user.FindFirst(SessionClaims.UserId)?.Value ?? "anonymous";
        return (await service.UpdateFieldAsync(id, path, request, userId, ct)).ToResult();
    }

    public static async Task<IResult> DeleteField(
        string id,
        string path,
        ClaimsPrincipal user,
        ISchemaService service,
        CancellationToken ct)
    {
        var userId = user.FindFirst(SessionClaims.UserId)?.Value ?? "anonymous";
        return (await service.DeleteFieldAsync(id, path, userId, ct)).ToResult();
    }

    public static async Task<IResult> ActivateSchema(
        string id,
        ClaimsPrincipal user,
        ISchemaService service,
        CancellationToken ct)
    {
        var userId = user.FindFirst(SessionClaims.UserId)?.Value ?? "anonymous";
        var result = await service.ActivateAsync(id, userId, ct);
        if (!result.IsSuccess)
        {
            return result.ToResult();
        }
        var schema = result.Value as SchemaResponse ?? throw new InvalidOperationException("Unexpected activate result");
        return Microsoft.AspNetCore.Http.Results.Ok(new { id, isActiveSource = schema.Schema.IsActiveSource });
    }

    public static async Task<IResult> ImportSchema(
        ImportSchemaRequest request,
        ClaimsPrincipal user,
        ISchemaService service,
        CancellationToken ct)
    {
        var userId = user.FindFirst(SessionClaims.UserId)?.Value ?? "anonymous";
        return (await service.ImportAsync(request, userId, ct)).ToResult();
    }

    public static async Task<IResult> DeleteSchema(
        string id,
        ClaimsPrincipal user,
        ISchemaService service,
        CancellationToken ct)
    {
        var userId = user.FindFirst(SessionClaims.UserId)?.Value ?? "anonymous";
        return (await service.DeleteAsync(id, userId, ct)).ToResult();
    }
}
