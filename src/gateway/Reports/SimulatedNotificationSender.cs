using System.Collections.Concurrent;

namespace Gateway.Reports;

public sealed class SimulatedNotificationSender : INotificationSender
{
    private readonly ConcurrentQueue<NotificationMessage> _sent = new();

    public IReadOnlyList<NotificationMessage> Sent => _sent.ToArray();

    public Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }
}
