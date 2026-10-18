using System.Text.Json;

namespace Gateway.Configuration;

public sealed class SettingsProvider : ISettingsProvider
{
    private readonly Dictionary<string, JsonElement> _sections = new();
    private readonly Dictionary<string, int> _versions = new();

    public event Action<string>? Changed;

    public JsonElement? GetSection(string sectionName)
    {
        return _sections.TryGetValue(sectionName, out var element) ? element : null;
    }

    public bool TryGetValue(string sectionName, string key, out JsonElement value)
    {
        value = default;
        if (!_sections.TryGetValue(sectionName, out var section))
        {
            return false;
        }
        return section.ValueKind == JsonValueKind.Object && section.TryGetProperty(key, out value);
    }

    public int GetVersion(string sectionName)
    {
        return _versions.TryGetValue(sectionName, out var version) ? version : 0;
    }

    public void UpdateSection(string sectionName, JsonElement values, int version)
    {
        _sections[sectionName] = values.Clone();
        _versions[sectionName] = version;
        Changed?.Invoke(sectionName);
    }

    public void RemoveSection(string sectionName)
    {
        _sections.Remove(sectionName);
        _versions.Remove(sectionName);
        Changed?.Invoke(sectionName);
    }

    public void ResetAll()
    {
        _sections.Clear();
        _versions.Clear();
        Changed?.Invoke("*");
    }
}
