using Gateway.Rules;

namespace Gateway.Handlers;

public sealed class BusinessRuleRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Scope { get; set; } = "Global";
    public string? ScopeValue { get; set; }
    public int Priority { get; set; } = 100;
    public bool Enabled { get; set; } = true;
    public BusinessRuleConditionRequest Condition { get; set; } = new();
    public BusinessRuleActionRequest Action { get; set; } = new();
}

public sealed class BusinessRuleConditionRequest
{
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public IReadOnlyList<string> Values { get; set; } = [];
    public string? Combine { get; set; }
}

public sealed class BusinessRuleActionRequest
{
    public string Type { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
}
