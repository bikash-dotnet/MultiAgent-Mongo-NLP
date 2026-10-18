using System.Globalization;
using System.Text.Json;

namespace Gateway.Configuration;

public sealed record SettingValue(
    string Key,
    string Label,
    SettingType Type,
    JsonElement Value,
    SettingsSource Source,
    bool Editable,
    bool Secret,
    string? Description);

public sealed record SectionSnapshot(
    string SectionName,
    string Label,
    bool AdminOnly,
    IReadOnlyList<SettingValue> Values);

public sealed class SettingsService
{
    private readonly ISettingsStore _store;
    private readonly IConfiguration _configuration;

    public SettingsService(ISettingsStore store, IConfiguration configuration)
    {
        _store = store;
        _configuration = configuration;
    }

    public async Task<SectionSnapshot> GetSectionAsync(string sectionName, CancellationToken cancellationToken = default)
    {
        var section = SettingsCatalog.Sections.FirstOrDefault(s =>
            string.Equals(s.SectionName, sectionName, StringComparison.Ordinal));
        if (section is null)
        {
            throw new KeyNotFoundException($"Unknown configuration section '{sectionName}'.");
        }

        var document = await _store.GetAsync(sectionName, cancellationToken);
        JsonElement? dbRoot = document is null
            ? null
            : document.ValuesAsElement();

        bool HasDb(string shortKey) =>
            dbRoot.HasValue && dbRoot.Value.ValueKind == JsonValueKind.Object &&
            dbRoot.Value.TryGetProperty(shortKey, out _);

        JsonElement DbValue(string shortKey) =>
            dbRoot!.Value.GetProperty(shortKey);

        var values = new List<SettingValue>();
        foreach (var spec in section.Settings)
        {
            var shortKey = spec.Key[(spec.Key.IndexOf('.') + 1)..];
            var configPath = $"{sectionName}:{shortKey}";
            var bootstrap = _configuration[configPath];

            JsonElement effective;
            SettingsSource source;

            if (HasDb(shortKey))
            {
                effective = DbValue(shortKey);
                source = SettingsSource.Database;
            }
            else if (bootstrap is not null)
            {
                effective = ToJsonElement(bootstrap, spec.Type);
                source = SettingsSource.Default;
            }
            else
            {
                effective = spec.Default.ValueKind == JsonValueKind.Undefined
                    ? JsonSerializer.SerializeToElement((string?)null)
                    : spec.Default;
                source = SettingsSource.Default;
            }

            values.Add(new SettingValue(
                shortKey,
                spec.Label,
                spec.Type,
                effective,
                source,
                spec.Editable,
                spec.Secret,
                spec.Description));
        }

        return new SectionSnapshot(section.SectionName, section.Label, section.AdminOnly, values);
    }

    public async Task<IReadOnlyList<SectionSnapshot>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var snapshots = new List<SectionSnapshot>();
        foreach (var section in SettingsCatalog.Sections)
        {
            snapshots.Add(await GetSectionAsync(section.SectionName, cancellationToken));
        }
        return snapshots;
    }

    private static JsonElement ToJsonElement(string value, SettingType type)
    {
        return type switch
        {
            SettingType.String or SettingType.Select or SettingType.Url or SettingType.Password =>
                JsonSerializer.SerializeToElement(value),
            SettingType.Boolean => JsonSerializer.SerializeToElement(bool.Parse(value)),
            SettingType.Number or SettingType.Port =>
                JsonSerializer.SerializeToElement(int.Parse(value, CultureInfo.InvariantCulture)),
            SettingType.Double =>
                JsonSerializer.SerializeToElement(double.Parse(value, CultureInfo.InvariantCulture)),
            _ => JsonSerializer.SerializeToElement(value)
        };
    }
}
