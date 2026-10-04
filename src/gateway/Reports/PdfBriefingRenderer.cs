using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Gateway.Reports;

public static class PdfBriefingRenderer
{
    static PdfBriefingRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Render(string title, BriefingResult briefing)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(style => style.FontSize(11));
                page.Header().Text(title).SemiBold().FontSize(16);
                page.Content().PaddingVertical(10).Text(briefing.Text);
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("MultiAgent-Mongo-NLP");
                    text.Span(" | ");
                    text.CurrentPageNumber();
                });
            });
        });

        return document.GeneratePdf();
    }
}
