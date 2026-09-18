using System.Text.Json;

namespace Gateway.Nlp.Guardrails;

public sealed class InMemorySensitiveFieldRegistry : ISensitiveFieldRegistry
{
    private const string FileName = "field_registry.json";

    private readonly List<SensitiveFieldFlag> _flags;

    public InMemorySensitiveFieldRegistry(IEnumerable<SensitiveFieldFlag> flags)
    {
        _flags = flags.ToList();
    }

    public IReadOnlyList<SensitiveFieldFlag> All => _flags;

    public static InMemorySensitiveFieldRegistry LoadFromDirectory(string directory)
    {
        var path = Path.Combine(directory, FileName);
        using var document = JsonDocument.Parse(File.ReadAllText(path));

        var flags = new List<SensitiveFieldFlag>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            var roles = element.TryGetProperty("dataOwnerRoles", out var rolesElement) &&
                        rolesElement.ValueKind == JsonValueKind.Array
                ? rolesElement.EnumerateArray().Select(r => r.GetString() ?? string.Empty).ToList()
                : [];

            flags.Add(new SensitiveFieldFlag(
                element.GetProperty("path").GetString() ?? string.Empty,
                element.GetProperty("isSensitive").GetBoolean(),
                element.GetProperty("requiresApproval").GetBoolean(),
                roles));
        }

        return new InMemorySensitiveFieldRegistry(flags);
    }

    public bool TryGet(string fieldPath, out SensitiveFieldFlag flag)
    {
        var match = _flags.FirstOrDefault(f => IsRelated(f.Path, fieldPath));
        if (match is null)
        {
            flag = null!;
            return false;
        }

        flag = match;
        return true;
    }

    public IReadOnlyList<SensitiveFieldFlag> Match(IEnumerable<string> fieldPaths)
    {
        var matched = new List<SensitiveFieldFlag>();

        foreach (var fieldPath in fieldPaths)
        {
            foreach (var flag in _flags)
            {
                if (IsRelated(flag.Path, fieldPath) && !matched.Contains(flag))
                {
                    matched.Add(flag);
                }
            }
        }

        return matched;
    }

    private static bool IsRelated(string flaggedPath, string referencedPath)
    {
        if (string.IsNullOrWhiteSpace(referencedPath))
        {
            return false;
        }

        return string.Equals(flaggedPath, referencedPath, StringComparison.Ordinal) ||
               referencedPath.StartsWith(flaggedPath + ".", StringComparison.Ordinal) ||
               flaggedPath.StartsWith(referencedPath + ".", StringComparison.Ordinal);
    }
}
