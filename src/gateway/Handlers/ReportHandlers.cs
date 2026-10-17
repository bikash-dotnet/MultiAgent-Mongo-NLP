using Gateway.Conversations;
using Gateway.Endpoints;
using Gateway.Reports;

namespace Gateway.Handlers;

public static class ReportHandlers
{
    public static async Task<IResult> GetCsv(
        string id,
        ConversationOrchestrator conversations,
        CancellationToken cancellationToken)
    {
        var turn = await conversations.GetAsync(id, cancellationToken);
        if (turn is null)
        {
            return Results.NotFound();
        }

        if (turn.Step != "Complete")
        {
            return Results.Conflict(new { error = "conversation is not complete" });
        }

        var columns = turn.Columns?.Where(column => column.Selected).Select(column => column.Name).ToList() ?? [];
        var build = turn.Execution is not null
            ? ReportService.BuildCsv(turn.Execution.Rows, columns)
            : ReportService.BuildCsv(columns, turn.ApprovalRequired);
        if (!build.Ready)
        {
            return Results.Conflict(new { error = build.Reason });
        }

        return Results.Text(build.Csv, "text/csv");
    }

    public static async Task<IResult> GetXlsx(
        string id,
        ConversationOrchestrator conversations,
        ExportDeliveryService exports,
        CancellationToken cancellationToken)
    {
        var turn = await conversations.GetAsync(id, cancellationToken);
        if (turn is null)
        {
            return Results.NotFound();
        }

        if (turn.Step != "Complete")
        {
            return Results.Conflict(new { error = "conversation is not complete" });
        }

        var columns = turn.Columns?.Where(column => column.Selected).Select(column => column.Name).ToList() ?? [];
        if (turn.Execution is null || columns.Count == 0)
        {
            return Results.Conflict(new { error = "No columns were confirmed for this report." });
        }

        var result = exports.Xlsx(columns, turn.Execution.Rows);
        return Results.File(result.Content, result.ContentType, result.FileName);
    }

    public static async Task<IResult> GetPdf(
        string id,
        ConversationOrchestrator conversations,
        ExportDeliveryService exports,
        CancellationToken cancellationToken)
    {
        var turn = await conversations.GetAsync(id, cancellationToken);
        if (turn is null)
        {
            return Results.NotFound();
        }

        if (turn.Step != "Complete")
        {
            return Results.Conflict(new { error = "conversation is not complete" });
        }

        var columns = turn.Columns?.Where(column => column.Selected).Select(column => column.Name).ToList() ?? [];
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows = turn.Execution?.Rows ?? [];
        var briefing = NarrativeInsights.Standard(columns, rows);
        var result = exports.Pdf("Report briefing", briefing);
        return Results.File(result.Content, result.ContentType, result.FileName);
    }

    public static async Task<IResult> PostBriefing(
        string id,
        ConversationOrchestrator conversations,
        CancellationToken cancellationToken)
    {
        var turn = await conversations.GetAsync(id, cancellationToken);
        if (turn is null)
        {
            return Results.NotFound();
        }

        var columns = turn.Columns?.Where(column => column.Selected).Select(column => column.Name).ToList() ?? [];
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows = turn.Execution?.Rows ?? [];
        return Results.Ok(NarrativeInsights.Standard(columns, rows));
    }

    public static async Task<IResult> PostEmail(
        string id,
        EmailBriefingRequest? body,
        ConversationOrchestrator conversations,
        ExportDeliveryService exports,
        CancellationToken cancellationToken)
    {
        var turn = await conversations.GetAsync(id, cancellationToken);
        if (turn is null)
        {
            return Results.NotFound();
        }

        var recipient = string.IsNullOrWhiteSpace(body?.Recipient) ? turn.EmailPrefill : body!.Recipient;
        if (string.IsNullOrWhiteSpace(recipient))
        {
            return Results.BadRequest(new { error = "a recipient email is required" });
        }

        var columns = turn.Columns?.Where(column => column.Selected).Select(column => column.Name).ToList() ?? [];
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows = turn.Execution?.Rows ?? [];
        var briefing = NarrativeInsights.Standard(columns, rows);
        var sent = await exports.EmailBriefingAsync(recipient, "Report briefing", briefing, cancellationToken);
        return Results.Ok(new { sent, recipient, format = "PDF" });
    }
}
