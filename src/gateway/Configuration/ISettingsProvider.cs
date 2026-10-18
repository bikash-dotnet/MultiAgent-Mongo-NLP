using System.Collections.Generic;
using System.Text.Json;

namespace Gateway.Configuration;

public interface ISettingsProvider
{
    JsonElement? GetSection(string sectionName);

    bool TryGetValue(string sectionName, string key, out JsonElement value);

    int GetVersion(string sectionName);

    IReadOnlyList<string> GetSectionNames();

    void UpdateSection(string sectionName, JsonElement values, int version);

    void RemoveSection(string sectionName);

    void ResetAll();

    event Action<string>? Changed;
}
