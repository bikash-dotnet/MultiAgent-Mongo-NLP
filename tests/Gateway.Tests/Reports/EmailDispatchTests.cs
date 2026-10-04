using System.Text;
using Gateway.Reports;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Reports;

public class EmailDispatchTests
{
    [Fact]
    public void Csv_and_xlsx_carry_the_right_metadata()
    {
        var service = NewService(out _);
        var rows = new List<IReadOnlyDictionary<string, string?>>
        {
            new Dictionary<string, string?> { ["name"] = "a" }
        };

        var csv = service.Csv(["name"], rows);
        var xlsx = service.Xlsx(["name"], rows);

        Assert.Equal("CSV", csv.Format);
        Assert.Equal("text/csv", csv.ContentType);
        Assert.Equal("XLSX", xlsx.Format);
        Assert.StartsWith("application/vnd", xlsx.ContentType);
    }

    [Fact]
    public async Task Email_briefing_dispatches_a_pdf_attachment()
    {
        var service = NewService(out var sender);

        var sent = await service.EmailBriefingAsync(
            "lead@enterprise.com",
            "Weekly briefing",
            new BriefingResult("text", false),
            CancellationToken.None);

        Assert.True(sent);
        var message = Assert.Single(sender.Sent);
        Assert.Equal("lead@enterprise.com", message.Recipient);
        Assert.NotNull(message.Attachment);
        Assert.Equal("application/pdf", message.Attachment!.ContentType);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(message.Attachment.Content, 0, 5));
    }

    [Fact]
    public async Task Email_briefing_requires_a_recipient()
    {
        var service = NewService(out var sender);

        var sent = await service.EmailBriefingAsync(
            string.Empty,
            "Weekly briefing",
            new BriefingResult("text", false),
            CancellationToken.None);

        Assert.False(sent);
        Assert.Empty(sender.Sent);
    }

    private static ExportDeliveryService NewService(out CapturingEmailSender sender)
    {
        sender = new CapturingEmailSender();
        return new ExportDeliveryService(sender, Options.Create(new ReportsOptions()));
    }

    private sealed class CapturingEmailSender : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }
}
