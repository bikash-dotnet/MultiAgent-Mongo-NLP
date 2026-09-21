namespace Gateway.Nlp.Guardrails;

public sealed record ReportIntake(
    string RequesterEmail,
    string Purpose,
    string? ProjectCode,
    string ManagerEmail,
    IReadOnlyList<string> Columns,
    string DeliveryFormat)
{
    public const string Email = "EMAIL";

    public const string Csv = "CSV";
}
