using Gateway.Governance;
using Gateway.Persistence;
using Gateway.Rules;

namespace Gateway.Tests.Rules;

public class BusinessRuleEngineTests
{
    [Fact]
    public async Task Wildcard_condition_still_applies()
    {
        var engine = await EngineWith(
            Rule("always", "*", "equals", ["*"], RuleActions.CapRows, "max", "5"));

        var result = await engine.EvaluateAsync(Analyst(), schemaId: null);

        Assert.Equal(["always"], result.AppliedRuleIds);
        Assert.Equal(5, BusinessRuleEngine.CapRows(result.Effects));
    }

    [Fact]
    public async Task Matching_field_value_applies_the_rule()
    {
        var engine = await EngineWith(
            Rule("us-cap", "market", "equals", ["us"], RuleActions.CapRows, "max", "2"));

        var result = await engine.EvaluateAsync(
            Analyst(),
            schemaId: null,
            context: RuleEvaluationContext.From(
                ["market"],
                [new KeyValuePair<string, string>("market", "US")]));

        Assert.Equal(["us-cap"], result.AppliedRuleIds);
    }

    [Fact]
    public async Task Non_matching_field_value_skips_the_rule()
    {
        var engine = await EngineWith(
            Rule("us-cap", "market", "equals", ["us"], RuleActions.CapRows, "max", "2"));

        var result = await engine.EvaluateAsync(
            Analyst(),
            schemaId: null,
            context: RuleEvaluationContext.From(
                ["market"],
                [new KeyValuePair<string, string>("market", "eu")]));

        Assert.Empty(result.AppliedRuleIds);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public async Task Missing_field_skips_value_conditions()
    {
        var engine = await EngineWith(
            Rule("us-cap", "market", "eq", ["us"], RuleActions.CapRows, "max", "2"));

        var result = await engine.EvaluateAsync(Analyst(), schemaId: null);

        Assert.Empty(result.AppliedRuleIds);
    }

    [Fact]
    public async Task Field_presence_with_wildcard_values_applies()
    {
        var engine = await EngineWith(
            Rule("mask-price", "price", "equals", ["*"], RuleActions.MaskColumn, "field", "price"));

        var result = await engine.EvaluateAsync(
            Analyst(),
            schemaId: null,
            context: RuleEvaluationContext.From(["price"]));

        Assert.Equal(["mask-price"], result.AppliedRuleIds);
        Assert.Equal(["price"], BusinessRuleEngine.MaskedColumns(result.Effects));
    }

    [Fact]
    public async Task Field_presence_without_values_does_not_satisfy_equals()
    {
        var engine = await EngineWith(
            Rule("us-cap", "market", "equals", ["us"], RuleActions.CapRows, "max", "2"));

        var result = await engine.EvaluateAsync(
            Analyst(),
            schemaId: null,
            context: RuleEvaluationContext.From(["market"]));

        Assert.Empty(result.AppliedRuleIds);
    }

    [Fact]
    public async Task In_and_not_in_use_set_membership()
    {
        var engine = await EngineWith(
            Rule("in-us", "market", "in", ["us", "ca"], RuleActions.CapRows, "max", "3"),
            Rule("not-eu", "market", "notIn", ["eu"], RuleActions.MaskColumn, "field", "price"));

        var result = await engine.EvaluateAsync(
            Analyst(),
            schemaId: null,
            context: Context(("market", "us")));

        Assert.Equal(["in-us", "not-eu"], result.AppliedRuleIds);
    }

    [Fact]
    public async Task Contains_and_numeric_comparisons_work()
    {
        var engine = await EngineWith(
            Rule("wifi", "amenities", "contains", ["wifi"], RuleActions.SetDefault, "key", "sort"),
            Rule("expensive", "price", "greaterThan", ["100"], RuleActions.CapRows, "max", "1"),
            Rule("cheap", "price", "lessThan", ["10"], RuleActions.ExcludeColumn, "field", "price"));

        var result = await engine.EvaluateAsync(
            Analyst(),
            schemaId: null,
            context: Context(("amenities", "free wifi"), ("price", "150")));

        Assert.Equal(["expensive", "wifi"], result.AppliedRuleIds);
        Assert.Equal(1, BusinessRuleEngine.CapRows(result.Effects));
        Assert.Empty(BusinessRuleEngine.ExcludedColumns(result.Effects));
    }

    [Fact]
    public void Combine_all_requires_every_value()
    {
        var matching = RuleConditionEvaluator.Matches(
            new RuleCondition("tag", "equals", ["a", "a"], "all"),
            Analyst(),
            Context(("tag", "a")));
        var mismatch = RuleConditionEvaluator.Matches(
            new RuleCondition("tag", "equals", ["a", "b"], "all"),
            Analyst(),
            Context(("tag", "a")));
        var anyMatch = RuleConditionEvaluator.Matches(
            new RuleCondition("tag", "equals", ["a", "b"], "any"),
            Analyst(),
            Context(("tag", "a")));

        Assert.True(matching);
        Assert.False(mismatch);
        Assert.True(anyMatch);
    }

    [Fact]
    public async Task Matches_data_owner_role_uses_the_requester()
    {
        var engine = await EngineWith(
            Rule("owner", "role", "matchesDataOwnerRole", [], RuleActions.ExemptRole, "role", "Data Owner / Admin"));

        var owner = await engine.EvaluateAsync(Owner(), schemaId: null);
        var analyst = await engine.EvaluateAsync(Analyst(), schemaId: null);

        Assert.Equal(["owner"], owner.AppliedRuleIds);
        Assert.Empty(analyst.AppliedRuleIds);
    }

    [Fact]
    public async Task Scope_still_filters_before_conditions()
    {
        var engine = await EngineWith(
            new BusinessRule(
                "role-rule", "role-rule", null, RuleScope.Role, "Business Analyst", 10, true,
                new RuleCondition("*", "equals", ["*"], "all"),
                new RuleAction(RuleActions.CapRows, new Dictionary<string, string> { ["max"] = "4" }),
                1, DateTimeOffset.UtcNow, "system"));

        var analyst = await engine.EvaluateAsync(Analyst(), schemaId: null);
        var owner = await engine.EvaluateAsync(Owner(), schemaId: null);

        Assert.Equal(["role-rule"], analyst.AppliedRuleIds);
        Assert.Empty(owner.AppliedRuleIds);
    }

    [Fact]
    public async Task Disabled_rules_are_skipped_even_when_the_condition_matches()
    {
        var engine = await EngineWith(
            new BusinessRule(
                "off", "off", null, RuleScope.Global, null, 10, false,
                new RuleCondition("*", "equals", ["*"], "all"),
                new RuleAction(RuleActions.CapRows, new Dictionary<string, string> { ["max"] = "1" }),
                1, DateTimeOffset.UtcNow, "system"));

        var result = await engine.EvaluateAsync(Analyst(), schemaId: null);

        Assert.Empty(result.AppliedRuleIds);
    }

    private static RequesterContext Analyst() =>
        new("usr_1", "Bikash", "Business Analyst", null);

    private static RequesterContext Owner() =>
        new("usr_owner", "Owner", "Data Owner / Admin", null);

    private static RuleEvaluationContext Context(params (string Field, string Value)[] values) =>
        RuleEvaluationContext.From(values: values.Select(v => new KeyValuePair<string, string>(v.Field, v.Value)));

    private static BusinessRule Rule(
        string id,
        string field,
        string op,
        string[] values,
        string action,
        string paramKey,
        string paramValue) =>
        new(
            id,
            id,
            null,
            RuleScope.Global,
            null,
            10,
            true,
            new RuleCondition(field, op, values, "all"),
            new RuleAction(action, new Dictionary<string, string> { [paramKey] = paramValue }),
            1,
            DateTimeOffset.UtcNow,
            "system");

    private static async Task<BusinessRuleEngine> EngineWith(params BusinessRule[] rules)
    {
        var store = new BusinessRuleStore(new InMemoryDocumentStore());
        foreach (var rule in rules)
        {
            await store.UpsertAsync(rule, "system", CancellationToken.None);
        }

        return new BusinessRuleEngine(store);
    }
}
