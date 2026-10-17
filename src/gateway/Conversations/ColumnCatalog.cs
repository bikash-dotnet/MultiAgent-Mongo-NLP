using Gateway.Nlp;
using Gateway.Nlp.Guardrails;

namespace Gateway.Conversations;

public sealed class ColumnCatalog : IColumnCatalog
{
    public static readonly IReadOnlyList<string> Standard =
    [
        "name",
        "address.market",
        "price",
        "room_type",
        "accommodates",
        "review_scores.rating"
    ];

    private readonly ActiveSchemaProvider? _provider;

    public ColumnCatalog(ActiveSchemaProvider? provider = null)
    {
        _provider = provider;
    }

    public IReadOnlyList<ColumnOption> Available(string? mql)
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        var schemaFields = _provider?.Fields ?? [];
        if (schemaFields.Count > 0)
        {
            foreach (var field in schemaFields)
            {
                if (!string.IsNullOrWhiteSpace(field.Path) &&
                    !string.Equals(field.Path, "_id", StringComparison.Ordinal) &&
                    seen.Add(field.Path))
                {
                    names.Add(field.Path);
                }
            }
        }
        else
        {
            foreach (var name in Standard)
            {
                if (seen.Add(name))
                {
                    names.Add(name);
                }
            }
        }

        foreach (var path in MqlAnalyzer.Analyze(mql).FieldPaths)
        {
            if (seen.Add(path))
            {
                names.Add(path);
            }
        }

        return names.Select(name => new ColumnOption(name, true)).ToList();
    }
}
