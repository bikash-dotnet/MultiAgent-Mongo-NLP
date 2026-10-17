using Gateway.Auth;
using Gateway.Infrastructure.Filters;
using Gateway.Infrastructure.Results;
using Gateway.Schemas;
using Gateway.Services;
using System.Security.Claims;

namespace Gateway.Endpoints;

public static class SchemaEndpoints
{
    public static IEndpointRouteBuilder MapSchemaEndpoints(this IEndpointRouteBuilder app)
    {
        var readOnly = app.MapGroup("/api/schemas")
            .RequireAuthorization();

        readOnly.MapGet("", ListSchemas);
        readOnly.MapGet("/{id}", GetSchemaById);

        var mutating = app.MapGroup("/api/schemas")
            .RequireAuthorization()
            .AddEndpointFilter<DataOwnerOrAdminFilter>();

        mutating.MapPost("", CreateSchema);
        mutating.MapPut("/{id}", UpdateSchema);
        mutating.MapPost("/{id}/fields", AddField);
        mutating.MapPut("/{id}/fields/{path}", UpdateField);
        mutating.MapDelete("/{id}/fields/{path}", DeleteField);
        mutating.MapPost("/{id}/activate", ActivateSchema);
        mutating.MapPost("/import", ImportSchema);
        mutating.MapDelete("/{id}", DeleteSchema);

        return app;
    }

    static async Task<IResult> ListSchemas(ISchemaService service, CancellationToken ct) =>
        (await service.ListAsync(ct)).ToResult();

    static async Task<IResult> GetSchemaById(string id, ISchemaService service, CancellationToken ct) =>
        (await service.GetByIdAsync(id, ct)).ToResult();

    static async Task<IResult> CreateSchema(
        CreateSchemaRequest request,
        ClaimsPrincipal user,
        ISchemaService service,
        CancellationToken ct)
    {
        var userId = user.FindFirst(SessionClaims.UserId)?.Value ?? "anonymous";
        return (await service.CreateAsync(request, userId, ct)).ToResult();
    }

    static async Task<IResult> UpdateSchema(
        string id,
        UpdateSchemaRequest request,
        ClaimsPrincipal user,
        ISchemaService service,
        CancellationToken ct)
    {
        var userId = user.FindFirst(SessionClaims.UserId)?.Value ?? "anonymous";
        return (await service.UpdateAsync(id, request, userId, ct)).ToResult();
    }

    static async Task<IResult> AddField(
        string id,
        SchemaFieldRequest request,
        ClaimsPrincipal user,
        ISchemaService service,
        CancellationToken ct)
    {
        var userId = user.FindFirst(SessionClaims.UserId)?.Value ?? "anonymous";
        return (await service.AddFieldAsync(id, request, userId, ct)).ToResult();
    }

    static async Task<IResult> UpdateField(
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

    static async Task<IResult> DeleteField(
        string id,
        string path,
        ClaimsPrincipal user,
        ISchemaService service,
        CancellationToken ct)
    {
        var userId = user.FindFirst(SessionClaims.UserId)?.Value ?? "anonymous";
        return (await service.DeleteFieldAsync(id, path, userId, ct)).ToResult();
    }

    static async Task<IResult> ActivateSchema(
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

    static async Task<IResult> ImportSchema(
        ImportSchemaRequest request,
        ClaimsPrincipal user,
        ISchemaService service,
        CancellationToken ct)
    {
        var userId = user.FindFirst(SessionClaims.UserId)?.Value ?? "anonymous";
        return (await service.ImportAsync(request, userId, ct)).ToResult();
    }

    static async Task<IResult> DeleteSchema(
        string id,
        ClaimsPrincipal user,
        ISchemaService service,
        CancellationToken ct)
    {
        var userId = user.FindFirst(SessionClaims.UserId)?.Value ?? "anonymous";
        return (await service.DeleteAsync(id, userId, ct)).ToResult();
    }
}
