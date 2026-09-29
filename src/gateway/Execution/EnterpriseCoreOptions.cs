namespace Gateway.Execution;

public sealed class EnterpriseCoreOptions
{
    public const string SectionName = "EnterpriseCore";

    public string BaseUrl { get; set; } = string.Empty;

    public string QueryPath { get; set; } = "/api/query";
}
