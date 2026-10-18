using System.Text.Json;
using Gateway.Governance;

namespace Gateway.Rules;

public sealed class BusinessRuleEngine
{
    private readonly IBusinessRuleStore _store;

    public BusinessRuleEngine(IBusinessRuleStore store)
    {
        _store = store;
    }

    public async Task<RuleEvaluationResult> EvaluateAsync(
        RequesterContext requester,
        string? schemaId,
        CancellationToken cancellationToken = default)
    {
        var rules = await _store.ListAsync(cancellationToken);

        var applicable = rules
            .Where(rule => rule.Enabled)
            .Where(rule => rule.Scope switch
            {
                RuleScope.Global => true,
                RuleScope.Schema => string.Equals(rule.ScopeValue, schemaId, StringComparison.Ordinal),
                RuleScope.Role => string.Equals(rule.ScopeValue, requester.Role, StringComparison.Ordinal),
                _ => false
            })
            .OrderBy(rule => rule.Priority)
            .ThenBy(rule => rule.Id, StringComparer.Ordinal)
            .ToList();

        var effects = new List<RuleEffect>();
        var appliedRuleIds = new List<string>();

        foreach (var rule in applicable)
        {
            appliedRuleIds.Add(rule.Id);
            effects.Add(new RuleEffect(rule.Id, rule.Action.Type, rule.Action.Parameters));
        }

        return new RuleEvaluationResult(effects, appliedRuleIds);
    }

    public static string? GetParameter(RuleEffect effect, string key) =>
        effect.Parameters.TryGetValue(key, out var value) ? value : null;

    public static bool HasAction(IReadOnlyList<RuleEffect> effects, string actionType) =>
        effects.Any(e => string.Equals(e.ActionType, actionType, StringComparison.Ordinal));

    public static IReadOnlyList<RuleEffect> EffectsByAction(
        IReadOnlyList<RuleEffect> effects,
        string actionType) =>
        effects.Where(e => string.Equals(e.ActionType, actionType, StringComparison.Ordinal)).ToList();

    public static IReadOnlyList<string> DeniedOperators(IReadOnlyList<RuleEffect> effects) =>
        EffectsByAction(effects, RuleActions.DenyOperator)
            .Select(e => GetParameter(e, "operator")!)
            .Where(v => v is not null)
            .Cast<string>()
            .ToList();

    public static IReadOnlyList<string> MaskedColumns(IReadOnlyList<RuleEffect> effects) =>
        EffectsByAction(effects, RuleActions.MaskColumn)
            .Select(e => GetParameter(e, "field")!)
            .Where(v => v is not null)
            .Cast<string>()
            .ToList();

    public static IReadOnlyList<string> ExcludedColumns(IReadOnlyList<RuleEffect> effects) =>
        EffectsByAction(effects, RuleActions.ExcludeColumn)
            .Select(e => GetParameter(e, "field")!)
            .Where(v => v is not null)
            .Cast<string>()
            .ToList();

    public static int? CapRows(IReadOnlyList<RuleEffect> effects) =>
        EffectsByAction(effects, RuleActions.CapRows)
            .Select(e => GetParameter(e, "max"))
            .Where(v => int.TryParse(v, out var max))
            .Select(v => (int?)int.Parse(v!))
            .FirstOrDefault();

    public static IReadOnlyDictionary<string, string> SetDefaults(IReadOnlyList<RuleEffect> effects) =>
        EffectsByAction(effects, RuleActions.SetDefault)
            .ToDictionary(e => GetParameter(e, "key")!, e => GetParameter(e, "value")!, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> RequireApprovalFields(IReadOnlyList<RuleEffect> effects) =>
        EffectsByAction(effects, RuleActions.RequireApproval)
            .Select(e => GetParameter(e, "field")!)
            .Where(v => v is not null)
            .Cast<string>()
            .ToList();

    public static IReadOnlyList<string> ExemptRoles(IReadOnlyList<RuleEffect> effects) =>
        EffectsByAction(effects, RuleActions.ExemptRole)
            .Select(e => GetParameter(e, "role")!)
            .Where(v => v is not null)
            .Cast<string>()
            .ToList();
}
