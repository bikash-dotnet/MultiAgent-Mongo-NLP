using Gateway.Schemas;

namespace Gateway.Schemas;

public sealed record CreateSchemaRequest(
    string Name,
    SchemaDataSource DataSource,
    string Database,
    string Collection,
    IReadOnlyList<SchemaFieldRequest> Fields,
    string? Description = null);

public sealed record UpdateSchemaRequest(
    string Name,
    SchemaDataSource DataSource,
    string Database,
    string Collection,
    IReadOnlyList<SchemaFieldRequest> Fields,
    int Version,
    bool IsActiveSource,
    string? Description = null);

public sealed record SchemaFieldRequest(
    string Path,
    SchemaFieldType DataType,
    bool IsSensitive,
    bool RequiresApproval,
    IReadOnlyList<string>? DataOwnerRoles,
    string Description = "");

public sealed record ImportSchemaRequest(
    string? FieldsJson,
    string? SchemaTxt);

public sealed record SchemaResponse(
    SchemaDefinition Schema);

public sealed record SchemaListResponse(
    IReadOnlyList<SchemaSummary> Schemas);
