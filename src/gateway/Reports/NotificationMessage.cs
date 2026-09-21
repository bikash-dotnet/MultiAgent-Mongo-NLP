namespace Gateway.Reports;

public sealed record NotificationMessage(string Kind, string Recipient, string Subject, string Body);
