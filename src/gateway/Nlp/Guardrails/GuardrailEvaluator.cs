using Gateway.Governance;

namespace Gateway.Nlp.Guardrails;

public sealed class GuardrailEvaluator
{
    private readonly ActiveSchemaProvider _provider;
    private readonly IApprovalFlagStore _flags;

    public GuardrailEvaluator(ActiveSchemaProvider provider, IApprovalFlagStore flags)
    {
        _provider = provider;
        _flags = flags;
    }

    public GuardrailResult Evaluate(string? pipelineJson, string? requesterRole = null)
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

        // Fail open when no schema data has been loaded yet (registry not seeded).
        if (_provider.WhitelistFields.Count == 0 && _provider.SensitiveFlags.Count == 0)
        {
            return new GuardrailResult(GuardrailOutcome.Allowed, null, [], [], []);
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

        if (approvalFields.Count > 0 && _flags.Enabled)
        {
            var approvalFlagList = matched.Where(flag => flag.RequiresApproval).ToList();
            if (IsExempt(requesterRole, approvalFlagList))
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
