using Gateway.Auth;
using Gateway.Conversations;
using Gateway.Endpoints;
using Gateway.Reports;
using Gateway.Rules;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Gateway.Handlers;

public static class ReportHandlers
{
    public static async Task<IResult> GetCsv(
        string id,
        ClaimsPrincipal user,
        ConversationOrchestrator conversations,
        ColumnDefinitionService columnDefs,
        BusinessRuleEngine rules,
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

        var (columns, rows) = await PrepareExportAsync(turn, user, columnDefs, rules, cancellationToken);
        var build = turn.Execution is not null
            ? ReportService.BuildCsv(rows, columns)
            : ReportService.BuildCsv(columns, turn.ApprovalRequired);
        if (!build.Ready)
        {
            return Results.Conflict(new { error = build.Reason });
        }

        return Results.Text(build.Csv, "text/csv");
    }

    public static async Task<IResult> GetXlsx(
        string id,
        ClaimsPrincipal user,
        ConversationOrchestrator conversations,
        ColumnDefinitionService columnDefs,
        BusinessRuleEngine rules,
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

        var (columns, rows) = await PrepareExportAsync(turn, user, columnDefs, rules, cancellationToken);
        if (turn.Execution is null || columns.Count == 0)
        {
            return Results.Conflict(new { error = "No columns were confirmed for this report." });
        }

        var result = exports.Xlsx(columns, rows);
        return Results.File(result.Content, result.ContentType, result.FileName);
    }

    public static async Task<IResult> GetPdf(
        string id,
        ClaimsPrincipal user,
        ConversationOrchestrator conversations,
        ColumnDefinitionService columnDefs,
        BusinessRuleEngine rules,
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

        var (columns, rows) = await PrepareExportAsync(turn, user, columnDefs, rules, cancellationToken);
        if (turn.Execution is null || columns.Count == 0)
        {
            return Results.Conflict(new { error = "No columns were confirmed for this report." });
        }

        var fieldNames = columns.Select(c => c.Field).ToList();
        var briefing = NarrativeInsights.Standard(fieldNames, rows);
        var result = exports.Pdf("Report briefing", briefing);
        return Results.File(result.Content, result.ContentType, result.FileName);
    }

    public static async Task<IResult> PostBriefing(
        string id,
        ClaimsPrincipal user,
        ConversationOrchestrator conversations,
        ColumnDefinitionService columnDefs,
        BusinessRuleEngine rules,
        CancellationToken cancellationToken)
    {
        var turn = await conversations.GetAsync(id, cancellationToken);
        if (turn is null)
        {
            return Results.NotFound();
        }

        var (columns, rows) = await PrepareExportAsync(turn, user, columnDefs, rules, cancellationToken);
        var fieldNames = columns.Select(c => c.Field).ToList();
        IReadOnlyList<IReadOnlyDictionary<string, string?>> briefingRows = turn.Execution?.Rows ?? rows;
        return Results.Ok(NarrativeInsights.Standard(fieldNames, briefingRows));
    }

    public static async Task<IResult> PostEmail(
        string id,
        EmailBriefingRequest? body,
        ClaimsPrincipal user,
        ConversationOrchestrator conversations,
        ColumnDefinitionService columnDefs,
        BusinessRuleEngine rules,
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

        var (columns, rows) = await PrepareExportAsync(turn, user, columnDefs, rules, cancellationToken);
        var fieldNames = columns.Select(c => c.Field).ToList();
        IReadOnlyList<IReadOnlyDictionary<string, string?>> briefingRows = turn.Execution?.Rows ?? rows;
        var briefing = NarrativeInsights.Standard(fieldNames, briefingRows);
        var sent = await exports.EmailBriefingAsync(recipient, "Report briefing", briefing, cancellationToken);
        return Results.Ok(new { sent, recipient, format = "PDF" });
    }

    private static async Task<(IReadOnlyList<ColumnDefinition> Columns, IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows)> PrepareExportAsync(
        ConversationTurn turn,
        ClaimsPrincipal user,
        ColumnDefinitionService columnDefs,
        BusinessRuleEngine rules,
        CancellationToken cancellationToken)
    {
        var requester = SessionClaims.ToRequesterContext(user);
        var ruleResult = await rules.EvaluateAsync(requester, schemaId: null, cancellationToken: cancellationToken);
        var effects = ruleResult.Effects;

        var excluded = BusinessRuleEngine.ExcludedColumns(effects)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var masked = BusinessRuleEngine.MaskedColumns(effects)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var exemptRoles = BusinessRuleEngine.ExemptRoles(effects);
        var isExempt = exemptRoles.Contains(requester.Role, StringComparer.OrdinalIgnoreCase);
        var cap = BusinessRuleEngine.CapRows(effects);

        var allDefs = await columnDefs.GetDefinitionsAsync(cancellationToken);
        var byField = allDefs.ToDictionary(d => d.Field, StringComparer.OrdinalIgnoreCase);

        var selectedNames = (turn.Columns ?? [])
            .Where(c => c.Selected)
            .Select(c => c.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var exportCols = new List<ColumnDefinition>();
        foreach (var name in selectedNames)
        {
            if (excluded.Contains(name))
            {
                continue;
            }
            var def = byField.TryGetValue(name, out var d)
                ? d
                : new ColumnDefinition(
                    ColumnDefinition.NewId(), name, name, ColumnDataType.String, null,
                    exportCols.Count, true, true, false, null, 0, null, null);
            if (!def.Exportable)
            {
                continue;
            }
            exportCols.Add(def);
        }

        var rows = (turn.Execution?.Rows ?? [])
            .Select(row => row
                .Where(kv => !excluded.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value) as IReadOnlyDictionary<string, string?>)
            .ToList();

        if (!isExempt && masked.Count > 0)
        {
            rows = rows.Select(row =>
            {
                var maskedRow = new Dictionary<string, string?>();
                foreach (var kv in row)
                {
                    maskedRow[kv.Key] = masked.Contains(kv.Key) ? "***" : kv.Value;
                }
                return maskedRow as IReadOnlyDictionary<string, string?>;
            }).ToList();
        }

        if (cap is int capped && capped >= 0 && rows.Count > capped)
        {
            rows = rows.Take(capped).ToList();
        }

        return (exportCols, rows);
    }
}
