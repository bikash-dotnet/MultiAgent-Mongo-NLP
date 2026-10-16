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
        var names = new List<string>(Standard);
        var seen = new HashSet<string>(Standard, StringComparer.Ordinal);

        if (_provider is not null)
        {
            foreach (var path in _provider.WhitelistFields)
            {
                if (seen.Add(path))
                {
                    names.Add(path);
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
