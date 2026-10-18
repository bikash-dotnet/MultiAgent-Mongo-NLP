using Gateway.Governance;
using Gateway.Rules;

namespace Gateway.Nlp.Guardrails;

public sealed class GuardrailEvaluator
{
    private readonly ActiveSchemaProvider _provider;
    private readonly IApprovalFlagStore _flags;
    private readonly BusinessRuleEngine _rules;

    public GuardrailEvaluator(ActiveSchemaProvider provider, IApprovalFlagStore flags, BusinessRuleEngine rules)
    {
        _provider = provider;
        _flags = flags;
        _rules = rules;
    }

    public async Task<GuardrailResult> EvaluateAsync(
        string? pipelineJson,
        string? requesterRole = null,
        string? schemaId = null,
        RequesterContext? requester = null,
        CancellationToken cancellationToken = default)
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

        var ruleResult = await _rules.EvaluateAsync(requester ?? new RequesterContext(requesterRole ?? string.Empty, string.Empty, requesterRole ?? string.Empty, null), schemaId, cancellationToken);

        var deniedOperators = BusinessRuleEngine.DeniedOperators(ruleResult.Effects);
        var usedOperators = analysis.Operators;
        foreach (var op in usedOperators)
        {
            if (deniedOperators.Contains(op))
            {
                return new GuardrailResult(
                    GuardrailOutcome.Rejected,
                    $"operator '{op}' is denied by business rule",
                    [],
                    [],
                    [op]);
            }
        }

        if (_provider.WhitelistFields.Count == 0 && _provider.SensitiveFlags.Count == 0)
        {
            return new GuardrailResult(
                GuardrailOutcome.Rejected,
                "schema registry is not loaded",
                [],
                [],
                []);
        }

        var unknownFields = _provider
            .FindUnknown(analysis.FieldPaths)
            .Where(path => _provider.MatchSensitive([path]).Count == 0)
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

        var matched = _provider
            .MatchSensitive(analysis.FieldPaths)
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

        // Layer rule-based requireApproval on top of schema flags.
        var ruleRequireApproval = BusinessRuleEngine.RequireApprovalFields(ruleResult.Effects);
        foreach (var field in ruleRequireApproval)
        {
            if (!approvalFields.Contains(field))
            {
                approvalFields.Add(field);
            }
        }

        if (approvalFields.Count > 0 && _flags.Enabled)
        {
            var approvalFlagList = matched.Where(flag => flag.RequiresApproval).ToList();
            var ruleExemptRoles = BusinessRuleEngine.ExemptRoles(ruleResult.Effects);
            var isExempt = IsExempt(requesterRole, approvalFlagList) ||
                           (ruleExemptRoles.Count > 0 && ruleExemptRoles.Contains(requesterRole ?? string.Empty));

            if (isExempt)
            {
                return new GuardrailResult(
                    GuardrailOutcome.Allowed,
                    null,
                    sensitiveFields,
                    [],
                    [],
                    AccessRequest.ExemptionOwnerAccess);
            }

            return new GuardrailResult(
                GuardrailOutcome.PausedForApproval,
                $"sensitive field requires approval: {string.Join(", ", approvalFields)}",
                approvalFields,
                [],
                []);
        }

        return new GuardrailResult(GuardrailOutcome.Allowed, null, sensitiveFields, [], []);
    }

    private static bool IsExempt(string? requesterRole, IReadOnlyList<SensitiveFieldFlag> approvalFlags)
    {
        if (string.IsNullOrWhiteSpace(requesterRole) || approvalFlags.Count == 0)
        {
            return false;
        }

        return approvalFlags.All(flag =>
            flag.DataOwnerRoles.Any(ownerRole => RoleNormalizer.Matches(requesterRole, ownerRole)));
    }
}
