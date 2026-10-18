using Gateway.Rules;
using Gateway.Persistence;

namespace Gateway.Tests.Rules;

public class BusinessRuleStoreTests
{
    [Fact]
    public async Task Upsert_then_get_round_trips_with_version()
    {
        var backend = new InMemoryDocumentStore();
        var store = new BusinessRuleStore(backend);

        var rule = new BusinessRule(
            BusinessRule.NewId(), "cap-rows", "Cap rows at 100",
            RuleScope.Global, null, 100, true,
            new RuleCondition("market", "eq", ["us"], "all"),
            new RuleAction(RuleActions.CapRows, new Dictionary<string, string> { ["rows"] = "100" }),
            1, null, null);

        var stored = await store.UpsertAsync(rule, "user-1", CancellationToken.None);

        Assert.Equal(1, stored.Version);
        Assert.Equal("user-1", stored.UpdatedBy);
        Assert.NotNull(stored.UpdatedAt);

        var loaded = await store.GetAsync(stored.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal("cap-rows", loaded!.Name);
        Assert.Equal(1, loaded.Version);
    }

    [Fact]
    public async Task Upsert_increments_version()
    {
        var backend = new InMemoryDocumentStore();
        var store = new BusinessRuleStore(backend);

        var rule = new BusinessRule(
            BusinessRule.NewId(), "cap-rows", "Cap rows at 100",
            RuleScope.Global, null, 100, true,
            new RuleCondition("market", "eq", ["us"], "all"),
            new RuleAction(RuleActions.CapRows, new Dictionary<string, string> { ["rows"] = "100" }),
            1, null, null);

        var first = await store.UpsertAsync(rule, "user-1", CancellationToken.None);
        var second = await store.UpsertAsync(first with { Name = "cap-rows-v2", Priority = 200 }, "user-2", CancellationToken.None);

        Assert.Equal(2, second.Version);
        Assert.Equal("user-2", second.UpdatedBy);

        var loaded = await store.GetAsync(first.Id, CancellationToken.None);
        Assert.Equal(2, loaded!.Version);
        Assert.Equal("cap-rows-v2", loaded.Name);
    }

    [Fact]
    public async Task List_returns_all_rules()
    {
        var backend = new InMemoryDocumentStore();
        var store = new BusinessRuleStore(backend);

        await store.UpsertAsync(new BusinessRule(
            BusinessRule.NewId(), "rule-a", null, RuleScope.Global, null, 100, true,
            new RuleCondition("market", "eq", ["us"], "all"),
            new RuleAction(RuleActions.CapRows, new Dictionary<string, string> { ["rows"] = "100" }),
            1, null, null), "user-1", CancellationToken.None);

        await store.UpsertAsync(new BusinessRule(
            BusinessRule.NewId(), "rule-b", null, RuleScope.Global, null, 50, true,
            new RuleCondition("market", "eq", ["eu"], "all"),
            new RuleAction(RuleActions.CapRows, new Dictionary<string, string> { ["rows"] = "50" }),
            1, null, null), "user-1", CancellationToken.None);

        var all = await store.ListAsync(CancellationToken.None);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task Delete_removes_rule()
    {
        var backend = new InMemoryDocumentStore();
        var store = new BusinessRuleStore(backend);

        var rule = await store.UpsertAsync(new BusinessRule(
            BusinessRule.NewId(), "to-delete", null, RuleScope.Global, null, 100, true,
            new RuleCondition("market", "eq", ["us"], "all"),
            new RuleAction(RuleActions.CapRows, new Dictionary<string, string> { ["rows"] = "100" }),
            1, null, null), "user-1", CancellationToken.None);

        var deleted = await store.DeleteAsync(rule.Id, CancellationToken.None);
        Assert.True(deleted);

        var loaded = await store.GetAsync(rule.Id, CancellationToken.None);
        Assert.Null(loaded);

        var missing = await store.DeleteAsync("nonexistent", CancellationToken.None);
        Assert.False(missing);
    }
}
