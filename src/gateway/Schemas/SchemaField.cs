using System.Text.Json.Serialization;

namespace Gateway.Schemas;

[JsonConverter(typeof(JsonStringEnumConverter<SchemaFieldType>))]
public enum SchemaFieldType
{
    String,
    Number,
    Boolean,
    Date,
    Object,
    Array
}

public sealed record SchemaField(
    string Path,
    SchemaFieldType DataType = SchemaFieldType.String,
    bool IsSensitive = false,
    bool RequiresApproval = false,
    IReadOnlyList<string>? DataOwnerRoles = null,
    string? Description = null)
{
    public IReadOnlyList<string> Roles => DataOwnerRoles ?? [];
}
