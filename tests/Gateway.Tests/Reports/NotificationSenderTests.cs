using Gateway.Reports;

namespace Gateway.Tests.Reports;

public class NotificationSenderTests
{
    [Fact]
    public async Task Records_sent_messages_in_order()
    {
        var sender = new SimulatedNotificationSender();

        await sender.SendAsync(new NotificationMessage("report_email", "a@x.com", "subject", "body"));
        await sender.SendAsync(new NotificationMessage("manager_notification", "m@x.com", "subject", "body"));

        Assert.Equal(2, sender.Sent.Count);
        Assert.Equal("report_email", sender.Sent[0].Kind);
        Assert.Equal("m@x.com", sender.Sent[1].Recipient);
    }
}
