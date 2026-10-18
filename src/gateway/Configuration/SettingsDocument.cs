using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gateway.Configuration;

[JsonConverter(typeof(JsonStringEnumConverter<SettingsSource>))]
public sealed record SettingsDocument(
    string Id,
    JsonElement Values,
    int Version,
    SettingsSource Source = SettingsSource.Database,
    DateTimeOffset? UpdatedAt = null,
    string? UpdatedBy = null)
{
    public static string NewId(string sectionKey) => $"cfg_{sectionKey}";
}

public sealed record SettingsSummary(
    string Id,
    int Version,
    SettingsSource Source,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
