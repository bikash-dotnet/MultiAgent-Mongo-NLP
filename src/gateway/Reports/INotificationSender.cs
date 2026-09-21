namespace Gateway.Reports;

public interface INotificationSender
{
    IReadOnlyList<NotificationMessage> Sent { get; }

    Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default);
}
