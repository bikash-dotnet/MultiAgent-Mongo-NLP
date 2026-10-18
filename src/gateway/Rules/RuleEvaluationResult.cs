namespace Gateway.Rules;

public sealed record RuleEffect(
    string RuleId,
    string ActionType,
    IReadOnlyDictionary<string, string> Parameters);

public sealed record RuleEvaluationResult(
    IReadOnlyList<RuleEffect> Effects,
    IReadOnlyList<string> AppliedRuleIds);
