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

    public IReadOnlyList<ColumnOption> Available(string? mql)
    {
        var names = new List<string>(Standard);
        var seen = new HashSet<string>(Standard, StringComparer.Ordinal);

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
