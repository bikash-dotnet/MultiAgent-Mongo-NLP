namespace Gateway.Nlp.Guardrails;

public static class ReadOnlyRule
{
    public static readonly IReadOnlySet<string> BlockedOperators =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "$out",
            "$merge",
            "drop",
            "deleteMany",
            "deleteOne",
            "updateMany",
            "updateOne",
            "insertMany",
            "insertOne",
            "replaceOne",
            "findAndModify",
            "renameCollection",
            "dropDatabase",
            "create",
            "createIndex"
        };

    public static IReadOnlyList<string> FindViolations(MqlAnalysis analysis)
    {
        if (!analysis.IsParsable)
        {
            return [analysis.ParseError ?? "unparseable pipeline"];
        }

        return analysis.Operators
            .Where(BlockedOperators.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
