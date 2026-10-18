using System.Text.Json.Serialization;

namespace Gateway.Configuration;

[JsonConverter(typeof(JsonStringEnumConverter<SettingsSource>))]
public enum SettingsSource
{
    Default,
    Database
}
