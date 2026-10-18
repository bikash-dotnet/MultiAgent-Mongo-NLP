using System.Security.Claims;
using Gateway.Auth;
using Gateway.Audit;
using Gateway.Handlers;
using Gateway.Persistence;
using Gateway.Rules;

namespace Gateway.Tests.Handlers;

public class BusinessRuleHandlersTests
{
    private static ClaimsPrincipal AdminUser(string userId = "admin-1", string role = "Data Owner / Admin") =>
        new(new ClaimsIdentity(
        [
            new Claim(SessionClaims.UserId, userId),
            new Claim(SessionClaims.Name, "Admin User"),
            new Claim(SessionClaims.Email, "admin@enterprise.com"),
            new Claim(SessionClaims.Role, role)
        ]));

    private static BusinessRuleRequest NewRequest(string name = "test-rule") =>
        new()
        {
            Name = name,
            Description = "A test rule",
            Scope = "Global",
            ScopeValue = null,
            Priority = 100,
            Enabled = true,
            Condition = new BusinessRuleConditionRequest
            {
                Field = "market",
                Operator = "eq",
                Values = ["us"],
                Combine = "all"
            },
            Action = new BusinessRuleActionRequest
            {
                Type = RuleActions.CapRows,
                Parameters = new Dictionary<string, string> { ["rows"] = "100" }
            }
        };

    [Fact]
    public async Task Create_rule_returns_stored_rule_with_version_and_writes_audit()
    {
        var backend = new InMemoryDocumentStore();
        var store = new BusinessRuleStore(backend);
        var audit = new InMemoryAuditLogStore();

        var result = await BusinessRuleHandlers.CreateRule(
            NewRequest(), AdminUser(), store, audit, CancellationToken.None);

        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<BusinessRule>>(result);
        var rule = ok.Value;

        Assert.NotNull(rule);
        Assert.Equal(1, rule!.Version);
        Assert.Equal("admin-1", rule.UpdatedBy);
        Assert.Equal("test-rule", rule.Name);

        var logs = await audit.ListAsync(CancellationToken.None);
        Assert.Single(logs);
        Assert.Contains("business_rule.create", logs.Select(l => l.SchemaAction));
    }

    [Fact]
    public async Task Update_rule_requires_matching_version_and_writes_audit()
    {
        var backend = new InMemoryDocumentStore();
        var store = new BusinessRuleStore(backend);
        var audit = new InMemoryAuditLogStore();

        var created = await BusinessRuleHandlers.CreateRule(
            NewRequest("original"), AdminUser(), store, audit, CancellationToken.None);
        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<BusinessRule>>(created);
        var rule = ok.Value!;

        var updateRequest = NewRequest("updated");
        var result = await BusinessRuleHandlers.UpdateRule(
            rule.Id, updateRequest, AdminUser(), store, audit, CancellationToken.None, version: rule.Version);

        var ok2 = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<BusinessRule>>(result);
        var updated = ok2.Value!;

        Assert.Equal(2, updated.Version);
        Assert.Equal("updated", updated.Name);

        var logs = await audit.ListAsync(CancellationToken.None);
        Assert.Equal(2, logs.Count);
        Assert.Contains("business_rule.update", logs.Select(l => l.SchemaAction));
    }

    [Fact]
    public async Task Update_rule_with_stale_version_returns_conflict()
    {
        var backend = new InMemoryDocumentStore();
        var store = new BusinessRuleStore(backend);
        var audit = new InMemoryAuditLogStore();

        var created = await BusinessRuleHandlers.CreateRule(
            NewRequest("original"), AdminUser(), store, audit, CancellationToken.None);
        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<BusinessRule>>(created);
        var rule = ok.Value!;

        // Bump version by updating once
        await BusinessRuleHandlers.UpdateRule(
            rule.Id, NewRequest("v2"), AdminUser(), store, audit, CancellationToken.None, version: rule.Version);

        // Try updating with stale version
        var result = await BusinessRuleHandlers.UpdateRule(
            rule.Id, NewRequest("v3"), AdminUser(), store, audit, CancellationToken.None, version: rule.Version);

        var conflict = Assert.IsAssignableFrom<Microsoft.AspNetCore.Http.IStatusCodeHttpResult>(result);
        Assert.Equal(409, conflict.StatusCode);
    }

    [Fact]
    public async Task Get_rule_returns_not_found_for_missing_id()
    {
        var backend = new InMemoryDocumentStore();
        var store = new BusinessRuleStore(backend);

        var result = await BusinessRuleHandlers.GetRule("nonexistent", store, CancellationToken.None);

        var notFound = Assert.IsAssignableFrom<Microsoft.AspNetCore.Http.IStatusCodeHttpResult>(result);
        Assert.Equal(404, notFound.StatusCode);
    }

    [Fact]
    public async Task Delete_rule_removes_rule_and_writes_audit()
    {
        var backend = new InMemoryDocumentStore();
        var store = new BusinessRuleStore(backend);
        var audit = new InMemoryAuditLogStore();

        var created = await BusinessRuleHandlers.CreateRule(
            NewRequest("to-delete"), AdminUser(), store, audit, CancellationToken.None);
        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<BusinessRule>>(created);
        var rule = ok.Value!;

        var result = await BusinessRuleHandlers.DeleteRule(
            rule.Id, AdminUser(), store, audit, CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.NoContent>(result);

        var loaded = await store.GetAsync(rule.Id, CancellationToken.None);
        Assert.Null(loaded);

        var logs = await audit.ListAsync(CancellationToken.None);
        Assert.Equal(2, logs.Count);
        Assert.Contains("business_rule.delete", logs.Select(l => l.SchemaAction));
    }

    [Fact]
    public async Task List_rules_returns_all_rules_ordered_by_priority()
    {
        var backend = new InMemoryDocumentStore();
        var store = new BusinessRuleStore(backend);
        var audit = new InMemoryAuditLogStore();

        var low = NewRequest("low-priority");
        low.Priority = 200;
        var high = NewRequest("high-priority");
        high.Priority = 50;

        await BusinessRuleHandlers.CreateRule(low, AdminUser(), store, audit, CancellationToken.None);
        await BusinessRuleHandlers.CreateRule(high, AdminUser(), store, audit, CancellationToken.None);

        var result = await BusinessRuleHandlers.ListRules(store, CancellationToken.None);
        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<IOrderedEnumerable<BusinessRule>>>(
            result);
        var rules = ok.Value!.ToList();

        Assert.Equal(2, rules.Count);
        Assert.Equal(50, rules[0].Priority);
        Assert.Equal(200, rules[1].Priority);
    }
}
