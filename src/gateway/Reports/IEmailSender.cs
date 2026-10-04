namespace Gateway.Reports;

public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

public sealed record EmailMessage(
    string Recipient,
    string Subject,
    string Body,
    EmailAttachment? Attachment = null);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
