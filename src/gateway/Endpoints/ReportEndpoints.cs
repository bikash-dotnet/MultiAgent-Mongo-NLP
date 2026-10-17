using Gateway.Handlers;

namespace Gateway.Endpoints;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var conversations = app.MapGroup("/api/conversations/{id}")
            .RequireAuthorization();

        conversations.MapGet("report.csv", ReportHandlers.GetCsv);

        conversations.MapGet("report.xlsx", ReportHandlers.GetXlsx);

        conversations.MapGet("report.pdf", ReportHandlers.GetPdf);

        conversations.MapPost("report/briefing", ReportHandlers.PostBriefing);

        conversations.MapPost("report/email", ReportHandlers.PostEmail);

        return app;
    }
}
