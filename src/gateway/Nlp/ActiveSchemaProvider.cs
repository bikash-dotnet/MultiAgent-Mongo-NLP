using Gateway.Nlp.Guardrails;
using Gateway.Persistence;
using Gateway.Schemas;

namespace Gateway.Nlp;

public sealed class ActiveSchemaProvider
{
    private readonly ISchemaRegistry _registry;
    private readonly object _lock = new();
    private IReadOnlyList<string> _whitelistFields = [];
    private IReadOnlyList<SensitiveFieldFlag> _sensitiveFlags = [];

    public ActiveSchemaProvider(ISchemaRegistry registry)
    {
        _registry = registry;
        _ = RefreshAsync();
    }

    public IReadOnlyList<string> WhitelistFields => _whitelistFields;

    public IReadOnlyList<SensitiveFieldFlag> SensitiveFlags => _sensitiveFlags;

    public async Task RefreshAsync()
    {
        await RefreshInternalAsync();
    }

    public bool IsDirty => false;

    private async Task RefreshInternalAsync()
    {
        var schemas = await _registry.ListAsync();

        var fieldPaths = new List<string>();
        var pathSet = new HashSet<string>(StringComparer.Ordinal);
        var flags = new List<SensitiveFieldFlag>();

        foreach (var schema in schemas)
        {
            foreach (var field in schema.Fields)
            {
                if (pathSet.Add(field.Path))
                {
                    fieldPaths.Add(field.Path);
                }

                if (field.IsSensitive || field.RequiresApproval)
                {
                    flags.Add(new SensitiveFieldFlag(
                        field.Path, field.IsSensitive, field.RequiresApproval, field.Roles));
                }
            }
        }
        fieldPaths.Add("_id");

        lock (_lock)
        {
            _whitelistFields = fieldPaths.AsReadOnly();
            _sensitiveFlags = flags.AsReadOnly();
        }
    }

    public bool IsKnown(string fieldPath)
    {
        if (string.IsNullOrWhiteSpace(fieldPath))
        {
            return false;
        }

        return _whitelistFields.Any(known =>
            string.Equals(known, fieldPath, StringComparison.Ordinal) ||
            fieldPath.StartsWith(known + ".", StringComparison.Ordinal) ||
            known.StartsWith(fieldPath + ".", StringComparison.Ordinal));
    }

    public IReadOnlyList<string> FindUnknown(IEnumerable<string> fieldPaths)
    {
        return fieldPaths
            .Where(path => !IsKnown(path))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public IReadOnlyList<SensitiveFieldFlag> MatchSensitive(IEnumerable<string> fieldPaths)
    {
        var matched = new List<SensitiveFieldFlag>();

        foreach (var fieldPath in fieldPaths)
        {
            foreach (var flag in _sensitiveFlags)
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