using System.Text;
using Gateway.Reports;

namespace Gateway.Tests.Reports;

public class PdfBriefingRendererTests
{
    [Fact]
    public void Render_produces_pdf_bytes()
    {
        var bytes = PdfBriefingRenderer.Render("Briefing", new BriefingResult("This report contains 2 records.", false));

        Assert.True(bytes.Length > 100);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
    }
}
