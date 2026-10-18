using System.Text.Json.Serialization;

namespace Gateway.Rules;

public sealed record BusinessRule(
    string Id,
    string Name,
    string? Description,
    RuleScope Scope,
    string? ScopeValue,
    int Priority,
    bool Enabled,
    RuleCondition Condition,
    RuleAction Action,
    int Version,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy)
{
    public static string NewId() => Guid.NewGuid().ToString("N");

    public BusinessRule withId(string id) => this with { Id = id };
    public BusinessRule withVersion(int version) => this with { Version = version };
}

public enum RuleScope
{
    Global,
    Schema,
    Role
}

public sealed record RuleCondition(
    string Field,
    string Operator,
    IReadOnlyList<string> Values,
    string Combine = "all");

public sealed record RuleAction(
    string Type,
    IReadOnlyDictionary<string, string> Parameters);

public static class RuleActions
{
    public const string SetDefault = "setDefault";
    public const string CapRows = "capRows";
    public const string MaskColumn = "maskColumn";
    public const string ExcludeColumn = "excludeColumn";
    public const string RequireApproval = "requireApproval";
    public const string ExemptRole = "exemptRole";
    public const string DenyOperator = "denyOperator";
}
