using System.Text;
using Microsoft.Extensions.Options;

namespace Gateway.Reports;

public sealed class ExportDeliveryService
{
    public const string CsvContentType = "text/csv";
    public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const string PdfContentType = "application/pdf";

    private readonly IEmailSender _email;

    public ExportDeliveryService(IEmailSender email, IOptions<ReportsOptions> options)
    {
        _email = email;
    }

    public ExportResult Csv(IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyDictionary<string, string?>> rows)
    {
        var content = Encoding.UTF8.GetBytes(CsvExporter.Export(rows, columns));
        return new ExportResult("CSV", content, CsvContentType, "report.csv");
    }

    public ExportResult Xlsx(IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyDictionary<string, string?>> rows)
    {
        var content = XlsxExporter.Export(rows, columns);
        return new ExportResult("XLSX", content, XlsxContentType, "report.xlsx");
    }

    public ExportResult Pdf(string title, BriefingResult briefing)
    {
        var content = PdfBriefingRenderer.Render(title, briefing);
        return new ExportResult("PDF", content, PdfContentType, "briefing.pdf");
    }

    public async Task<bool> EmailBriefingAsync(
        string recipient,
        string title,
        BriefingResult briefing,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipient))
        {
            return false;
        }

        var pdf = Pdf(title, briefing);
        await _email.SendAsync(
            new EmailMessage(
                recipient,
                title,
                briefing.Text,
                new EmailAttachment(pdf.FileName, pdf.ContentType, pdf.Content)),
            cancellationToken);

        return true;
    }
}
