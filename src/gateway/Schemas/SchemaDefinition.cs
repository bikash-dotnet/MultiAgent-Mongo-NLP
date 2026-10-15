using System.Text.Json.Serialization;

namespace Gateway.Schemas;

[JsonConverter(typeof(JsonStringEnumConverter<SchemaDataSource>))]
public enum SchemaDataSource
{
    Mongo,
    EnterpriseCoreREST
}

public sealed record SchemaDefinition(
    string Id,
    string Name,
    SchemaDataSource DataSource,
    string Database,
    string Collection,
    IReadOnlyList<SchemaField> Fields,
    bool IsActiveSource = false,
    int Version = 1,
    string? Description = null,
    DateTimeOffset? CreatedAt = null,
    DateTimeOffset? UpdatedAt = null,
    string? UpdatedBy = null)
{
    public static string NewId() => "sch_" + Guid.NewGuid().ToString("N")[..12];
}

public sealed record SchemaSummary(
    string Id,
    string Name,
    SchemaDataSource DataSource,
    int FieldCount,
    bool IsActiveSource,
    int Version,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
