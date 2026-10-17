using Gateway.Auth;
using Gateway.Conversations;
using Gateway.Reports;
using System.Security.Claims;

namespace Gateway.Endpoints;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/conversations/{id}/report.csv", async (
            string id,
            ConversationOrchestrator conversations,
            CancellationToken cancellationToken) =>
        {
            var turn = await conversations.GetAsync(id, cancellationToken);
            if (turn is null)
            {
                return Microsoft.AspNetCore.Http.Results.NotFound();
            }

            if (turn.Step != "Complete")
            {
                return Microsoft.AspNetCore.Http.Results.Conflict(new { error = "conversation is not complete" });
            }

            var columns = turn.Columns?.Where(column => column.Selected).Select(column => column.Name).ToList() ?? [];
            var build = turn.Execution is not null
                ? ReportService.BuildCsv(turn.Execution.Rows, columns)
                : ReportService.BuildCsv(columns, turn.ApprovalRequired);
            if (!build.Ready)
            {
                return Microsoft.AspNetCore.Http.Results.Conflict(new { error = build.Reason });
            }

            return Microsoft.AspNetCore.Http.Results.Text(build.Csv, "text/csv");
        }).RequireAuthorization();

        app.MapGet("/api/conversations/{id}/report.xlsx", async (
            string id,
            ConversationOrchestrator conversations,
            ExportDeliveryService exports,
            CancellationToken cancellationToken) =>
        {
            var turn = await conversations.GetAsync(id, cancellationToken);
            if (turn is null)
            {
                return Microsoft.AspNetCore.Http.Results.NotFound();
            }

            if (turn.Step != "Complete")
            {
                return Microsoft.AspNetCore.Http.Results.Conflict(new { error = "conversation is not complete" });
            }

            var columns = turn.Columns?.Where(column => column.Selected).Select(column => column.Name).ToList() ?? [];
            if (turn.Execution is null || columns.Count == 0)
            {
                return Microsoft.AspNetCore.Http.Results.Conflict(new { error = "No columns were confirmed for this report." });
            }

            var result = exports.Xlsx(columns, turn.Execution.Rows);
            return Microsoft.AspNetCore.Http.Results.File(result.Content, result.ContentType, result.FileName);
        }).RequireAuthorization();

        app.MapGet("/api/conversations/{id}/report.pdf", async (
            string id,
            ConversationOrchestrator conversations,
            ExportDeliveryService exports,
            CancellationToken cancellationToken) =>
        {
            var turn = await conversations.GetAsync(id, cancellationToken);
            if (turn is null)
            {
                return Microsoft.AspNetCore.Http.Results.NotFound();
            }

            if (turn.Step != "Complete")
            {
                return Microsoft.AspNetCore.Http.Results.Conflict(new { error = "conversation is not complete" });
            }

            var columns = turn.Columns?.Where(column => column.Selected).Select(column => column.Name).ToList() ?? [];
            IReadOnlyList<IReadOnlyDictionary<string, string?>> rows = turn.Execution?.Rows ?? [];
            var briefing = NarrativeInsights.Standard(columns, rows);
            var result = exports.Pdf("Report briefing", briefing);
            return Microsoft.AspNetCore.Http.Results.File(result.Content, result.ContentType, result.FileName);
        }).RequireAuthorization();

        app.MapPost("/api/conversations/{id}/report/briefing", async (
            string id,
            ConversationOrchestrator conversations,
            CancellationToken cancellationToken) =>
        {
            var turn = await conversations.GetAsync(id, cancellationToken);
            if (turn is null)
            {
                return Microsoft.AspNetCore.Http.Results.NotFound();
            }

            var columns = turn.Columns?.Where(column => column.Selected).Select(column => column.Name).ToList() ?? [];
            IReadOnlyList<IReadOnlyDictionary<string, string?>> rows = turn.Execution?.Rows ?? [];
            return Microsoft.AspNetCore.Http.Results.Ok(NarrativeInsights.Standard(columns, rows));
        }).RequireAuthorization();

        app.MapPost("/api/conversations/{id}/report/email", async (
            string id,
            EmailBriefingRequest? body,
            ConversationOrchestrator conversations,
            ExportDeliveryService exports,
            CancellationToken cancellationToken) =>
        {
            var turn = await conversations.GetAsync(id, cancellationToken);
            if (turn is null)
            {
                return Microsoft.AspNetCore.Http.Results.NotFound();
            }

            var recipient = string.IsNullOrWhiteSpace(body?.Recipient) ? turn.EmailPrefill : body!.Recipient;
            if (string.IsNullOrWhiteSpace(recipient))
            {
                return Microsoft.AspNetCore.Http.Results.BadRequest(new { error = "a recipient email is required" });
            }

            var columns = turn.Columns?.Where(column => column.Selected).Select(column => column.Name).ToList() ?? [];
            IReadOnlyList<IReadOnlyDictionary<string, string?>> rows = turn.Execution?.Rows ?? [];
            var briefing = NarrativeInsights.Standard(columns, rows);
            var sent = await exports.EmailBriefingAsync(recipient, "Report briefing", briefing, cancellationToken);
            return Microsoft.AspNetCore.Http.Results.Ok(new { sent, recipient, format = "PDF" });
        }).RequireAuthorization();

        return app;
    }
}
