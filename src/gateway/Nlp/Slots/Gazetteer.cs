using System.Text.Json;

namespace Gateway.Nlp.Slots;

public sealed class Gazetteer
{
    private Gazetteer(
        IReadOnlyDictionary<string, string> marketAliases,
        IReadOnlyDictionary<string, string> amenityAliases)
    {
        MarketAliases = marketAliases;
        AmenityAliases = amenityAliases;
    }

    public IReadOnlyDictionary<string, string> MarketAliases { get; }
    public IReadOnlyDictionary<string, string> AmenityAliases { get; }

    public static Gazetteer LoadFromDirectory(string directory)
    {
        var markets = Load(Path.Combine(directory, "markets.json"));
        var amenities = Load(Path.Combine(directory, "amenities.json"));
        return new Gazetteer(BuildAliases(markets), BuildAliases(amenities));
    }

    private static List<GazetteerEntry> Load(string path)
    {
        var doc = JsonDocument.Parse(File.ReadAllText(path));
        var prop = doc.RootElement.EnumerateObject().First();
        return prop.Value.EnumerateArray()
            .Select(e => new GazetteerEntry
            {
                Canonical = e.GetProperty("canonical").GetString()!,
                Aliases = e.GetProperty("aliases").EnumerateArray().Select(a => a.GetString()!).ToList()
            })
            .ToList();
    }

    private static Dictionary<string, string> BuildAliases(List<GazetteerEntry> entries)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            foreach (var alias in entry.Aliases.Append(entry.Canonical.ToLowerInvariant()))
            {
                map[alias] = entry.Canonical;
            }
        }

        return map;
    }

    private sealed class GazetteerEntry
    {
        public string Canonical { get; set; } = "";
        public List<string> Aliases { get; set; } = new();
    }
}
