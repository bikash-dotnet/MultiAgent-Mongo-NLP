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
        foreach (var schema in schemas)
        {
            foreach (var field in schema.Fields)
            {
                if (pathSet.Add(field.Path))
                {
                    fieldPaths.Add(field.Path);
                }
            }
        }
        fieldPaths.Add("_id");
        _whitelistFields = fieldPaths.AsReadOnly();
    }
}