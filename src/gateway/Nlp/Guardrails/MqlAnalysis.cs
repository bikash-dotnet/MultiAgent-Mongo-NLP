namespace Gateway.Nlp.Guardrails;

public sealed record MqlAnalysis(
    bool IsParsable,
    string? ParseError,
    IReadOnlyList<string> FieldPaths,
    IReadOnlyList<string> Operators);
