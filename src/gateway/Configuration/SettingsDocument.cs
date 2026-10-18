using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gateway.Configuration;

public sealed record SettingsDocument(
    string Id,
    string ValuesJson,
    int Version,
    SettingsSource Source = SettingsSource.Database,
    DateTimeOffset? UpdatedAt = null,
    string? UpdatedBy = null)
{
    public static string NewId(string sectionKey) => $"cfg_{sectionKey}";

    public JsonElement ValuesAsElement()
    {
        return string.IsNullOrWhiteSpace(ValuesJson)
            ? default
            : JsonElement.Parse(ValuesJson).Clone();
    }
}

public sealed record SettingsSummary(
    string Id,
    int Version,
    SettingsSource Source,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
