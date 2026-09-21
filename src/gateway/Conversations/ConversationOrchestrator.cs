using System.Text.RegularExpressions;
using Gateway.Governance;
using Gateway.Nlp.Guardrails;
using Gateway.Nlp.Http;
using Gateway.Nlp.Orchestrator;
using Gateway.Nlp.Router;
using Gateway.Reports;
using Microsoft.Extensions.Options;

namespace Gateway.Conversations;

public sealed partial class ConversationOrchestrator
{
    private readonly INlpOrchestrator _nlp;
    private readonly IConversationStore _store;
    private readonly IAccessRequestStore _accessRequests;
    private readonly IApprovalFlagStore _flags;
    private readonly INotificationSender _notifications;
    private readonly IColumnCatalog _columns;
    private readonly IAgentEventSink _events;
    private readonly ReportsOptions _reports;
    private readonly TimeProvider _clock;

    public ConversationOrchestrator(
        INlpOrchestrator nlp,
        IConversationStore store,
        IAccessRequestStore accessRequests,
        IApprovalFlagStore flags,
        INotificationSender notifications,
        IColumnCatalog columns,
        IAgentEventSink events,
        IOptions<ReportsOptions> reports,
        TimeProvider clock)
    {
        _nlp = nlp;
        _store = store;
        _accessRequests = accessRequests;
        _flags = flags;
        _notifications = notifications;
        _columns = columns;
        _events = events;
        _reports = reports.Value;
        _clock = clock;
    }

    public async Task<ConversationTurn> StartAsync(
        string utterance,
        string sessionId,
        string? requesterEmail,
        CancellationToken cancellationToken = default)
    {
        var result = await _nlp.OrchestrateAsync(utterance, sessionId, cancellationToken);

        var kind = result.Kind;
        var mql = result.Mql;
        var demo = false;

        if (kind == NlpRouteKind.ComplexLlmFailed && _reports.DemoFallbackEnabled)
        {
            kind = NlpRouteKind.ComplexLlmRequired;
            mql = DemoReportPipeline.FromUtterance(utterance);
            demo = true;
        }

        var isReport = kind is NlpRouteKind.ComplexLlmRequired or NlpRouteKind.GovernancePaused;

        if (!isReport)
        {
            var direct = new ConversationState(
                string.Empty,
                sessionId,
                ConversationStep.Complete,
                utterance,
                mql,
                kind,
                result.AccessRequestId,
                result.SensitiveFields ?? [],
                false,
                false,
                new ReportIntakeDraft(),
                _clock.GetUtcNow(),
                _clock.GetUtcNow(),
                DirectMessage(result),
                NlpQueryResponse.From(result));

            var savedDirect = await _store.CreateAsync(direct, cancellationToken);
            return ToTurn(savedDirect);
        }

        var state = new ConversationState(
            string.Empty,
            sessionId,
            ConversationStep.Email,
            utterance,
            mql,
            kind,
            result.AccessRequestId,
            result.SensitiveFields ?? [],
            demo,
            false,
            new ReportIntakeDraft(RequesterEmail: string.IsNullOrWhiteSpace(requesterEmail) ? null : requesterEmail),
            _clock.GetUtcNow(),
            _clock.GetUtcNow());

        var saved = await _store.CreateAsync(state, cancellationToken);
        _events.Publish(new AgentEvent("conversation.started", "started", saved.Id));

        return ToTurn(saved);
    }

    public async Task<ConversationTurn?> AnswerAsync(
        string conversationId,
        ConversationAnswer answer,
        CancellationToken cancellationToken = default)
    {
        var state = await _store.GetAsync(conversationId, cancellationToken);
        if (state is null)
        {
            return null;
        }

        if (state.Step == ConversationStep.Complete)
        {
            return ToTurn(state) with { ValidationError = "This conversation is already complete." };
        }

        var draft = state.Draft;

        switch (state.Step)
        {
            case ConversationStep.Email:
            {
                var email = FirstNonEmpty(answer.Email, answer.Text) ?? draft.RequesterEmail;
                if (string.IsNullOrWhiteSpace(email) || !EmailPattern().IsMatch(email.Trim()))
                {
                    return ToTurn(state) with { ValidationError = "Enter a valid email address." };
                }

                draft = draft with { RequesterEmail = email.Trim() };
                state = state with { Step = ConversationStep.Purpose };
                break;
            }

            case ConversationStep.Purpose:
            {
                var purpose = FirstNonEmpty(answer.Purpose, answer.Text);
                if (string.IsNullOrWhiteSpace(purpose) || purpose.Trim().Length < 5 || purpose.Trim().Length > 500)
                {
                    return ToTurn(state) with { ValidationError = "Describe the purpose in 5 to 500 characters." };
                }

                var projectCode = FirstNonEmpty(answer.ProjectCode, null);
                if (projectCode is not null && !ProjectCodePattern().IsMatch(projectCode))
                {
                    return ToTurn(state) with { ValidationError = "The project code may contain letters, digits, and hyphens only." };
                }

                draft = draft with { Purpose = purpose.Trim(), ProjectCode = projectCode?.Trim() };
                state = state with { Step = ConversationStep.ManagerEmail };
                break;
            }

            case ConversationStep.ManagerEmail:
            {
                var manager = FirstNonEmpty(answer.ManagerEmail, answer.Text);
                if (string.IsNullOrWhiteSpace(manager) || !EmailPattern().IsMatch(manager.Trim()))
                {
                    return ToTurn(state) with { ValidationError = "Enter a valid manager email address." };
                }

                if (string.Equals(manager.Trim(), draft.RequesterEmail, StringComparison.OrdinalIgnoreCase))
                {
                    return ToTurn(state) with { ValidationError = "The manager email must differ from your own email." };
                }

                draft = draft with { ManagerEmail = manager.Trim() };
                state = state with { Step = ConversationStep.Columns };
                break;
            }

            case ConversationStep.Columns:
            {
                var requested = answer.Columns ?? [];
                var available = _columns.Available(state.Mql).Select(option => option.Name).ToHashSet(StringComparer.Ordinal);
                if (requested.Count == 0)
                {
                    return ToTurn(state) with { ValidationError = "Select at least one column." };
                }

                if (requested.Any(column => !available.Contains(column)))
                {
                    return ToTurn(state) with { ValidationError = "One or more columns are not available for this report." };
                }

                draft = draft with { Columns = requested };
                state = state with { Step = ConversationStep.Delivery };
                break;
            }

            case ConversationStep.Delivery:
            {
                var delivery = answer.Delivery?.Trim().ToUpperInvariant();
                if (delivery is not (ReportIntake.Email or ReportIntake.Csv))
                {
                    return ToTurn(state) with { ValidationError = "Choose email delivery or CSV download." };
                }

                draft = draft with { DeliveryFormat = delivery };
                state = await FinalizeAsync(state with { Draft = draft }, draft, cancellationToken);
                break;
            }
        }

        state = state with { Draft = draft, UpdatedAt = _clock.GetUtcNow() };
        var saved = await _store.UpdateAsync(state, cancellationToken);
        return ToTurn(saved);
    }

    public async Task<ConversationTurn?> GetAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        var state = await _store.GetAsync(conversationId, cancellationToken);
        return state is null ? null : ToTurn(state);
    }

    private async Task<ConversationState> FinalizeAsync(
        ConversationState state,
        ReportIntakeDraft draft,
        CancellationToken cancellationToken)
    {
        var intake = new ReportIntake(
            draft.RequesterEmail!,
            draft.Purpose!,
            draft.ProjectCode,
            draft.ManagerEmail!,
            draft.Columns ?? [],
            draft.DeliveryFormat!);

        var approvalRequired = state.Kind == NlpRouteKind.GovernancePaused;

        if (approvalRequired && state.AccessRequestId is not null)
        {
            var existing = await _accessRequests.GetAsync(state.AccessRequestId, cancellationToken);
            if (existing is not null)
            {
                await _accessRequests.UpdateAsync(
                    existing with { Intake = intake, Justification = intake.Purpose },
                    cancellationToken);
            }

            await _notifications.SendAsync(
                new NotificationMessage(
                    "manager_notification",
                    intake.ManagerEmail,
                    $"Sensitive data request from {intake.RequesterEmail}",
                    $"{intake.RequesterEmail} has requested sensitive data ({string.Join(", ", state.SensitiveFields)}) and it is awaiting approval."),
                cancellationToken);

            _events.Publish(new AgentEvent("governance.request_enriched", "enriched", state.AccessRequestId));
            _events.Publish(new AgentEvent("governance.manager_notified", "notified", intake.ManagerEmail));
        }
        else if (intake.DeliveryFormat == ReportIntake.Email)
        {
            await _notifications.SendAsync(
                new NotificationMessage(
                    "report_email",
                    intake.RequesterEmail,
                    "Your report is ready",
                    $"Report with columns {string.Join(", ", intake.Columns)}."),
                cancellationToken);

            _events.Publish(new AgentEvent("report.email_simulated", "sent", intake.RequesterEmail));
        }

        if (intake.DeliveryFormat == ReportIntake.Csv && !approvalRequired)
        {
            _events.Publish(new AgentEvent("report.ready", "ready", state.Id));
        }

        _events.Publish(new AgentEvent("conversation.completed", "completed", state.Id));

        return state with
        {
            Step = ConversationStep.Complete,
            ApprovalRequired = approvalRequired,
            UpdatedAt = _clock.GetUtcNow()
        };
    }

    private ConversationTurn ToTurn(ConversationState state)
    {
        var control = state.Step switch
        {
            ConversationStep.Email => ConversationControl.Email,
            ConversationStep.Purpose => ConversationControl.Purpose,
            ConversationStep.Columns => ConversationControl.Columns,
            ConversationStep.Delivery => ConversationControl.Delivery,
            _ => ConversationControl.None
        };

        var columns = state.Step == ConversationStep.Columns
            ? _columns.Available(state.Mql)
            : null;

        var downloadable = state.Step == ConversationStep.Complete
            && !state.ApprovalRequired
            && state.Draft.DeliveryFormat == ReportIntake.Csv;

        return new ConversationTurn(
            state.Id,
            state.Step.ToString(),
            state.Kind.ToString(),
            state.Message ?? StepMessage(state),
            control.ToString().ToLowerInvariant(),
            state.Step == ConversationStep.Email ? state.Draft.RequesterEmail : null,
            columns,
            [ReportIntake.Email, ReportIntake.Csv],
            state.AccessRequestId,
            state.ApprovalRequired,
            downloadable,
            state.DemoReport,
            state.Result,
            null);
    }

    private string StepMessage(ConversationState state)
    {
        return state.Step switch
        {
            ConversationStep.Email => "I can build that report. Which email should receive it?",
            ConversationStep.Purpose => "What is the purpose of this report?",
            ConversationStep.ManagerEmail => "Add your manager's email so they can be notified.",
            ConversationStep.Columns => "Confirm the columns for your report.",
            ConversationStep.Delivery => "Email the report, or download it as CSV?",
            ConversationStep.Complete => CompletionMessage(state),
            _ => state.Utterance
        };
    }

    private string CompletionMessage(ConversationState state)
    {
        if (state.ApprovalRequired)
        {
            return $"Approval is required because this report uses sensitive fields ({string.Join(", ", state.SensitiveFields)}). "
                + $"Your manager {state.Draft.ManagerEmail} has been notified, and request {state.AccessRequestId} is pending lead approval.";
        }

        if (state.Draft.DeliveryFormat == ReportIntake.Csv)
        {
            return "Your report is ready. Use the Download CSV button to save it.";
        }

        return $"Your report has been emailed to {state.Draft.RequesterEmail}.";
    }

    private static string DirectMessage(NlpRouteResult result)
    {
        return result.Kind switch
        {
            NlpRouteKind.CacheHit => "Here is the cached query for that request.",
            NlpRouteKind.SimpleMql => "Here is the query for that request.",
            NlpRouteKind.ClarifyRequired => result.Question ?? "Could you clarify your request?",
            NlpRouteKind.Rejected => $"This request was blocked: {result.GuardrailReason}",
            NlpRouteKind.ComplexLlmFailed => $"The language model is unavailable. {result.Error}",
            _ => "Here is the result for that request."
        };
    }

    private static string? FirstNonEmpty(string? first, string? second)
    {
        return !string.IsNullOrWhiteSpace(first) ? first : !string.IsNullOrWhiteSpace(second) ? second : null;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^[A-Za-z0-9-]+$")]
    private static partial Regex ProjectCodePattern();
}
