using Gateway.Governance;

namespace Gateway.Nlp.Guardrails;

public sealed class GuardrailEvaluator
{
    private readonly SchemaWhitelist _whitelist;
    private readonly ISensitiveFieldRegistry _registry;
    private readonly IApprovalFlagStore _flags;

    public GuardrailEvaluator(SchemaWhitelist whitelist, ISensitiveFieldRegistry registry, IApprovalFlagStore flags)
    {
        _whitelist = whitelist;
        _registry = registry;
        _flags = flags;
    }

    public GuardrailResult Evaluate(string? pipelineJson)
    {
        var analysis = MqlAnalyzer.Analyze(pipelineJson);

        var violations = ReadOnlyRule.FindViolations(analysis);
        if (violations.Count > 0)
        {
            return new GuardrailResult(
                GuardrailOutcome.Rejected,
                analysis.IsParsable
                    ? $"read-only violation: {string.Join(", ", violations)}"
                    : analysis.ParseError,
                [],
                [],
                violations);
        }

        var unknownFields = _whitelist
            .FindUnknown(analysis.FieldPaths)
            .Where(path => _registry.Match([path]).Count == 0)
            .ToList();

        if (unknownFields.Count > 0)
        {
            return new GuardrailResult(
                GuardrailOutcome.Rejected,
                $"unknown field path: {string.Join(", ", unknownFields)}",
                [],
                unknownFields,
                []);
        }

        var matched = _registry
            .Match(analysis.FieldPaths)
            .Where(flag => flag.IsSensitive)
            .ToList();

        var sensitiveFields = matched
            .Select(flag => flag.Path)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var approvalFields = matched
            .Where(flag => flag.RequiresApproval)
            .Select(flag => flag.Path)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (approvalFields.Count > 0 && _flags.Enabled)
        {
            return new GuardrailResult(
                GuardrailOutcome.PausedForApproval,
                $"sensitive field requires approval: {string.Join(", ", approvalFields)}",
                approvalFields,
                [],
                []);
        }

        return new GuardrailResult(GuardrailOutcome.Allowed, null, sensitiveFields, [], []);
    }
}
