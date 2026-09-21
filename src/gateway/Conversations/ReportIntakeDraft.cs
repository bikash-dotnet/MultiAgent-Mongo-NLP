namespace Gateway.Conversations;

public sealed record ReportIntakeDraft(
    string? RequesterEmail = null,
    string? Purpose = null,
    string? ProjectCode = null,
    string? ManagerEmail = null,
    IReadOnlyList<string>? Columns = null,
    string? DeliveryFormat = null);
