using System.Text.Json;
using Gateway.Nlp.Guardrails;

namespace Gateway.Schemas;

public static class SchemaSeeder
{
    private const string DefaultId = "sch_airbnb_listings";
    private const string DefaultName = "Airbnb Listings";
    private const string DefaultDatabase = "sample_airbnb";
    private const string DefaultCollection = "listingsAndReviews";

    private static readonly string SchemaPath =
        Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Prompts", "schema.txt");

    private static readonly string RegistryDirectory =
        Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Schema");

    public static SchemaDefinition BuildDefault()
    {
        var schemaFields = ParseSchemaFile(SchemaPath);
        var registryFlags = LoadRegistryFlags(RegistryDirectory);

        var fields = new List<SchemaField>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (path, dataType) in schemaFields)
        {
            if (seen.Add(path))
            {
                fields.Add(new SchemaField(path, dataType));
            }
        }

        foreach (var flag in registryFlags)
        {
            if (seen.Add(flag.Path))
            {
                fields.Add(new SchemaField(
                    flag.Path,
                    SchemaFieldType.String,
                    flag.IsSensitive,
                    flag.RequiresApproval,
                    flag.DataOwnerRoles));
            }
        }

        return new SchemaDefinition(
            DefaultId,
            DefaultName,
            SchemaDataSource.Mongo,
            DefaultDatabase,
            DefaultCollection,
            fields,
            true,
            1,
            "Default schema seeded from field_registry.json and schema.txt.");
    }

    private static IReadOnlyList<(string Path, SchemaFieldType DataType)> ParseSchemaFile(string path)
    {
        var fields = new List<(string, SchemaFieldType)>();

        if (!File.Exists(path))
        {
            return fields;
        }

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith("- ", StringComparison.Ordinal))
            {
                continue;
            }

            var entry = line[2..];
            var colon = entry.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            var fieldPath = entry[..colon].Trim();
            var rest = entry[(colon + 1)..].Trim();
            var paren = rest.IndexOf('(');
            if (paren >= 0)
            {
                rest = rest[..paren].Trim();
            }

            if (fieldPath.Length > 0)
            {
                fields.Add((fieldPath, ParseDataType(rest)));
            }
        }

        return fields;
    }

    private static SchemaFieldType ParseDataType(string type)
    {
        var lower = type.ToLowerInvariant();
        if (lower.StartsWith("array"))
        {
            return SchemaFieldType.Array;
        }

        return lower switch
        {
            "string" => SchemaFieldType.String,
            "number" => SchemaFieldType.Number,
            "boolean" => SchemaFieldType.Boolean,
            "date" => SchemaFieldType.Date,
            "object" => SchemaFieldType.Object,
            _ => SchemaFieldType.String
        };
    }

    private static IReadOnlyList<SensitiveFieldFlag> LoadRegistryFlags(string directory)
    {
        var path = Path.Combine(directory, "field_registry.json");
        if (!File.Exists(path))
        {
            return [];
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var flags = new List<SensitiveFieldFlag>();

        foreach (var element in document.RootElement.EnumerateArray())
        {
            var roles = element.TryGetProperty("dataOwnerRoles", out var rolesElement) &&
                        rolesElement.ValueKind == JsonValueKind.Array
                ? rolesElement.EnumerateArray().Select(r => r.GetString() ?? string.Empty).Where(r => r.Length > 0).ToList()
                : [];

            flags.Add(new SensitiveFieldFlag(
                element.GetProperty("path").GetString() ?? string.Empty,
                element.GetProperty("isSensitive").GetBoolean(),
                element.GetProperty("requiresApproval").GetBoolean(),
                roles));
        }

        return flags;
    }
}
