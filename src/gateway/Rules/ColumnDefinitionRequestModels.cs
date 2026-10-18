using System.Text.Json;

namespace Gateway.Rules;

public sealed class ColumnDefinitionRequest
{
    public string Field { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public ColumnDataType DataType { get; set; } = ColumnDataType.String;
    public string? Format { get; set; }
    public int Order { get; set; }
    public bool VisibleByDefault { get; set; } = true;
    public bool Exportable { get; set; } = true;
    public bool Sensitive { get; set; }
    public string? Width { get; set; }
}

public sealed class ColumnDefinitionReorderRequest
{
    public IReadOnlyList<string> Ids { get; set; } = [];
}
