using Gateway.Nlp.Guardrails;
using Gateway.Schemas;

namespace Gateway.Nlp;

public sealed class ActiveSchemaProvider
{
    private readonly ISchemaRegistry _registry;
    private readonly ILogger<ActiveSchemaProvider>? _logger;
    private readonly object _gate = new();
    private IReadOnlyList<string> _whitelistFields = [];
    private IReadOnlyList<SensitiveFieldFlag> _sensitiveFlags = [];
    private IReadOnlyList<SchemaField> _fields = [];
    private SchemaDefinition? _active;

    public ActiveSchemaProvider(ISchemaRegistry registry, ILogger<ActiveSchemaProvider>? logger = null)
    {
        _registry = registry;
        _logger = logger;
    }

    public SchemaDefinition? ActiveSchema
    {
        get
        {
            lock (_gate)
            {
                return _active;
            }
        }
    }

    public IReadOnlyList<string> WhitelistFields
    {
        get
        {
            lock (_gate)
            {
                return _whitelistFields;
            }
        }
    }

    public IReadOnlyList<SensitiveFieldFlag> SensitiveFlags
    {
        get
        {
            lock (_gate)
            {
                return _sensitiveFlags;
            }
        }
    }

    public IReadOnlyList<SchemaField> Fields
    {
        get
        {
            lock (_gate)
            {
                return _fields;
            }
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var schema = await _registry.GetActiveAsync(cancellationToken);
            var fields = schema?.Fields ?? [];

            var paths = new List<string>();
            var pathSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in fields)
            {
                if (!string.IsNullOrWhiteSpace(field.Path) && pathSet.Add(field.Path))
                {
                    paths.Add(field.Path);
                }
            }

            if (pathSet.Add("_id"))
            {
                paths.Add("_id");
            }

            var flags = fields
                .Where(field => field.IsSensitive || field.RequiresApproval)
                .Select(field => new SensitiveFieldFlag(
                    field.Path, field.IsSensitive, field.RequiresApproval, field.Roles))
                .ToList();

            lock (_gate)
            {
                _active = schema;
                _fields = fields.ToList().AsReadOnly();
                _whitelistFields = paths.AsReadOnly();
                _sensitiveFlags = flags.AsReadOnly();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to refresh the active schema; continuing with the previous snapshot.");
        }
    }

    public bool IsKnown(string fieldPath)
    {
        if (string.IsNullOrWhiteSpace(fieldPath))
        {
            return false;
        }

        return WhitelistFields.Any(known =>
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
        var flags = SensitiveFlags;

        foreach (var fieldPath in fieldPaths)
        {
            foreach (var flag in flags)
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
