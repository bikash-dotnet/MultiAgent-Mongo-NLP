using System.Text.Json;

namespace Gateway.Configuration;

public sealed record UpdateConfigSectionRequest(
    IReadOnlyDictionary<string, JsonElement> Values,
    int Version,
    string? Notes = null);

public sealed record SettingValueResponse(
    string Key,
    string Label,
    string Type,
    object Value,
    SettingsSource Source,
    bool Editable,
    bool Secret,
    string? Description);

public sealed record ConfigSectionResponse(
    string SectionName,
    string Label,
    bool AdminOnly,
    IReadOnlyList<SettingValueResponse> Values);

public sealed record ConfigSchemaResponse(
    IReadOnlyList<SettingsSectionSchema> Sections);

public sealed record SettingsSectionSchema(
    string SectionName,
    string Label,
    bool AdminOnly,
    IReadOnlyList<SettingsFieldSchema> Settings);

public sealed record SettingsFieldSchema(
    string Key,
    string Label,
    string Type,
    bool Editable,
    bool Secret,
    object? Default,
    int? Min,
    int? Max,
    double? MinD,
    double? MaxD,
    IReadOnlyList<string>? AllowedValues,
    string? Description);

public sealed record ConfigResetRequest(bool Confirm);
