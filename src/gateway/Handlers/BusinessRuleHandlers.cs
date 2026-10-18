using Gateway.Auth;
using Gateway.Rules;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Claims;

namespace Gateway.Handlers;

public static class BusinessRuleHandlers
{
    public static async Task<IResult> ListRules(
        IBusinessRuleStore store,
        CancellationToken ct)
    {
        var rules = await store.ListAsync(ct);
        return Results.Ok(rules.OrderBy(r => r.Priority).ThenBy(r => r.Id, StringComparer.Ordinal));
    }

    public static async Task<IResult> GetRule(
        string id,
        IBusinessRuleStore store,
        CancellationToken ct)
    {
        var rule = await store.GetAsync(id, ct);
        if (rule is null)
        {
            return Results.NotFound(new { error = $"Business rule '{id}' not found." });
        }
        return Results.Ok(rule);
    }

    public static async Task<IResult> CreateRule(
        BusinessRuleRequest request,
        ClaimsPrincipal user,
        IBusinessRuleStore store,
        CancellationToken ct)
    {
        var userId = user.FindFirst(SessionClaims.UserId)?.Value ?? "anonymous";

        var scope = Enum.Parse<RuleScope>(request.Scope);
        var condition = new RuleCondition(
            request.Condition.Field,
            request.Condition.Operator,
            request.Condition.Values,
            request.Condition.Combine ?? "all");

        var action = new RuleAction(request.Action.Type, request.Action.Parameters);

        var rule = new BusinessRule(
            BusinessRule.NewId(),
            request.Name,
            request.Description,
            scope,
            request.ScopeValue,
            request.Priority,
            request.Enabled,
            condition,
            action,
            1, DateTimeOffset.UtcNow, userId);

        var stored = await store.UpsertAsync(rule, userId, ct);
        return Results.Ok(stored);
    }

    public static async Task<IResult> UpdateRule(
        string id,
        BusinessRuleRequest request,
        ClaimsPrincipal user,
        IBusinessRuleStore store,
        CancellationToken ct,
        [FromQuery] int version)
    {
        var userId = user.FindFirst(SessionClaims.UserId)?.Value ?? "anonymous";

        var current = await store.GetAsync(id, ct);
        if (current is null)
        {
            return Results.NotFound(new { error = $"Business rule '{id}' not found." });
        }

        if (current.Version != version)
        {
            return Results.Conflict(new { error = $"Business rule '{id}' has been modified by another client." });
        }

        var scope = Enum.Parse<RuleScope>(request.Scope);
        var condition = new RuleCondition(
            request.Condition.Field,
            request.Condition.Operator,
            request.Condition.Values,
            request.Condition.Combine ?? "all");

        var action = new RuleAction(request.Action.Type, request.Action.Parameters);

        var rule = current with
        {
            Name = request.Name,
            Description = request.Description,
            Scope = scope,
            ScopeValue = request.ScopeValue,
            Priority = request.Priority,
            Enabled = request.Enabled,
            Condition = condition,
            Action = action,
            Version = current.Version + 1,
            UpdatedAt = DateTimeOffset.UtcNow,
            UpdatedBy = userId
        };

        var stored = await store.UpsertAsync(rule, userId, ct);
        return Results.Ok(stored);
    }

    public static async Task<IResult> DeleteRule(
        string id,
        IBusinessRuleStore store,
        CancellationToken ct)
    {
        var deleted = await store.DeleteAsync(id, ct);
        if (!deleted)
        {
            return Results.NotFound(new { error = $"Business rule '{id}' not found." });
        }
        return Results.NoContent();
    }
}
