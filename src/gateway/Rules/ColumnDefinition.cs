using System.Text.Json.Serialization;

namespace Gateway.Rules;

public sealed record ColumnDefinition(
    string Id,
    string Field,
    string Label,
    ColumnDataType DataType,
    string? Format,
    int Order,
    bool VisibleByDefault,
    bool Exportable,
    bool Sensitive,
    string? Width,
    int Version,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy)
{
    public static string NewId() => Guid.NewGuid().ToString("N");

    public ColumnDefinition withId(string id) => this with { Id = id };
    public ColumnDefinition withVersion(int version) => this with { Version = version };
}

[JsonConverter(typeof(JsonStringEnumConverter<ColumnDataType>))]
public enum ColumnDataType
{
    String,
    Number,
    Currency,
    Boolean,
    Date
}
