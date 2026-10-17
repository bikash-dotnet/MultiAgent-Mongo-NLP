using Gateway.Infrastructure.Results;
using Gateway.Schemas;

namespace Gateway.Services;

public interface ISchemaService
{
    Task<ApiResult<SchemaListResponse>> ListAsync(CancellationToken cancellationToken = default);

    Task<ApiResult<SchemaResponse>> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<ApiResult<SchemaResponse>> CreateAsync(
        CreateSchemaRequest request,
        string userId,
        CancellationToken cancellationToken = default);

    Task<ApiResult<SchemaResponse>> UpdateAsync(
        string id,
        UpdateSchemaRequest request,
        string userId,
        CancellationToken cancellationToken = default);

    Task<ApiResult<SchemaResponse>> AddFieldAsync(
        string id,
        SchemaFieldRequest request,
        string userId,
        CancellationToken cancellationToken = default);

    Task<ApiResult<SchemaResponse>> UpdateFieldAsync(
        string id,
        string path,
        SchemaFieldRequest request,
        string userId,
        CancellationToken cancellationToken = default);

    Task<ApiResult<SchemaResponse>> DeleteFieldAsync(
        string id,
        string path,
        string userId,
        CancellationToken cancellationToken = default);

    Task<ApiResult<SchemaResponse>> ActivateAsync(
        string id,
        string userId,
        CancellationToken cancellationToken = default);

    Task<ApiResult<SchemaResponse>> ImportAsync(
        ImportSchemaRequest request,
        string userId,
        CancellationToken cancellationToken = default);

    Task<ApiResult<SchemaResponse>> DeleteAsync(
        string id,
        string userId,
        CancellationToken cancellationToken = default);
}
