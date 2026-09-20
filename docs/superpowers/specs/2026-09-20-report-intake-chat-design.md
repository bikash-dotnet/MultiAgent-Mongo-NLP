# Report Intake Chat — Guided Conversation and Flag-Gated Approval Design

**Date:** 2026-09-20
**Status:** Draft for review
**Owner:** Bikash Ranjan Nayak
**Deliverable:** Chat-based report intake in the Angular SPA plus server-driven conversation, approval flag, and in-memory CSV export in the ASP.NET Core gateway

---

## 1. Goal

Replace the single-shot "Run query" panel with a chat assistant. When a user asks a query
that produces a report, the assistant collects the delivery email, the purpose, the
manager notification email, the desired columns, and the delivery choice, then starts the
approval process when the query touches sensitive fields. A global, runtime-mutable flag
controls whether approval is enforced, so governance can be switched on or off at any time
without a redeploy.

## 2. Audience and success criteria

- **Primary audience:** business analysts and data owners using the workspace.
- **Success looks like:** a user types a complex query, answers four short follow-up
  questions in the chat, confirms the columns, chooses email or CSV, and receives either a
  downloaded CSV or a clear explanation that approval has started. A data owner can toggle
  the approval gate live and see the behaviour change on the next request.

## 3. Scope

**In scope**

- Chat thread UI (user and assistant messages, inline step controls) replacing the current
  single input and raw pipeline output.
- Server-driven conversation state with resume-after-refresh.
- The five intake steps: email, purpose, manager email, columns, delivery.
- Runtime-mutable global approval flag with a REST endpoint and a role-restricted UI toggle.
- Access request enrichment with the collected intake when the query is sensitive.
- Simulated manager notification and simulated report email (no SMTP).
- In-memory CSV export from deterministic demo rows.
- A labelled demo report path so the flow works when no LLM key is configured.

**Out of scope**

- Real MongoDB execution of the generated pipeline (Sprint 6).
- Real SMTP delivery, PDF, and XLSX export (Sprint 7).
- The approver decision UI (approve or reject) and override portal (Sprint 5). This slice
  creates the pending request and explains the process; it does not resolve it.
- Durable storage. Both the conversation store and the access request store remain
  in-memory.
- Any change to the read-only and schema whitelist guardrail rules.

## 4. Confirmed decisions

| Topic | Decision |
| --- | --- |
| Scope | Full vertical slice: chat UI, conversation state, submission, approval flag, CSV export |
| Approval switch | One global runtime flag (`Governance:ApprovalEnabled`), on or off at any time |
| Flow trigger | Report-producing queries only; simple lookups answer directly |
| Approval driver | Sensitivity only; delivery choice does not change whether approval is required |
| Manager email | Notification recipient that a user's request was raised; the approver is separate and may be the same person |
| Columns | Derived from the generated query fields plus a standard default set, all pre-selected |
| Purpose | One required purpose text plus an optional project code |
| Email | The signed-in user's email; pre-filled from the session and asked only when absent; also used for delivery |
| Delivery realism | Real in-memory CSV from demo rows; email send simulated |
| Conversation state | Server-driven step machine |
| Flag control | Runtime store, REST endpoint, and a UI toggle visible only to specific role claims |
| Offline demo | A clearly labelled demo report path when the LLM is unavailable |

## 5. Conversation flow

A conversation has one of these steps in order. Each step is validated server-side; an
invalid answer returns the same step with a validation message and does not advance.

| # | Step | Assistant prompt | Control | Validation |
| --- | --- | --- | --- | --- |
| 1 | `Email` | "Which email should receive this report?" | Email input, pre-filled from the session email | Non-empty, valid email shape. A missing session email makes this a required answer. |
| 2 | `Purpose` | "What is the purpose of this report?" | Text area plus an optional project code field | Purpose 5 to 500 characters. Project code 0 to 40 characters, letters, digits, hyphen. |
| 3 | `ManagerEmail` | "Add your manager's email so they can be notified." | Email input | Non-empty, valid email shape, and different from the requester email. |
| 4 | `Columns` | "Confirm the columns for your report." | Checkbox list, standard set pre-selected | At least one column, and every column must be in the available set. |
| 5 | `Delivery` | "Email the report, or download it as CSV?" | Radio: Email report, Download CSV | One of `EMAIL` or `CSV`. |
| 6 | `Complete` | Summary plus approval outcome and next step | None | Not applicable. |

Notes:

- The unsigned email is accepted at step 1; trimming is applied and comparison is
  case-insensitive.
- Step 3 rejects the requester's own email because the notification is addressed to a
  manager about a report requested by someone under them.
- Step 4 offers the union of the field paths referenced by the generated query and the
  standard column set, de-duplicated and ordered with the standard set first. Every
  available column is selected by default. The standard column set is `name`,
  `address.market`, `price`, `room_type`, `accommodates`, and `review_scores.rating`.

## 6. Trigger rules

The conversation layer inspects the route kind returned by the existing NLP orchestrator.

| Route kind | Behaviour |
| --- | --- |
| `ComplexLlmRequired` | Start the guided intake. |
| `GovernancePaused` | Start the guided intake and enrich the existing access request on completion. |
| `ComplexLlmFailed` | Start the guided intake through the labelled demo path when the demo path is enabled; otherwise show the error. |
| `SimpleMql` | Answer directly with the generated query; no intake. |
| `CacheHit` | Answer directly; no intake. |
| `ClarifyRequired` | Show the clarifying question as an assistant message; the next user message starts a new query. |
| `Rejected` | Show the guardrail reason; no intake. |

All existing `POST /api/nlp/query` behaviour is unchanged. The conversation layer calls the
existing orchestrator internally.

## 7. Approval flag and sensitivity

- `IApprovalFlagStore` is an in-memory singleton with an initial value read from
  `Governance:ApprovalEnabled` (default `true`).
- `GuardrailEvaluator` consults the store. The order of checks is unchanged: unparseable,
  blocked operators, and unknown fields are always rejected. For sensitive fields the
  evaluator now separates "sensitive" from "requires approval":
  - approval flag on and a matched field has `requires_approval`: `PausedForApproval`.
  - approval flag off: `Allowed`, with the sensitive field paths still returned so the
    gateway can record that sensitive data was in scope.
- Read-only enforcement and the schema whitelist are never relaxed by the flag.
- When a sensitive request completes with the flag on, the gateway creates or enriches an
  access request with status `PENDING_LEAD` and attaches the intake. The approver is the
  `lead_user_id` from the requester's session, matching the BRD default; the manager email
  is a separate notification recipient and may refer to the same person.
- When the flag is off, no access request is created and the report is delivered directly.

## 8. Delivery, CSV, and email

- `DemoListingSource` produces deterministic listing rows for the report (a fixed set
  covering a small number of markets). The same input always yields the same rows.
- `CsvExporter` builds a CSV string in memory containing only the confirmed columns, with a
  header row, correct quoting for values containing commas or quotes, and no disk writes.
- `GET /api/conversations/{id}/report.csv`:
  - returns `200` with `text/csv` when the report may be delivered;
  - returns `409` with the approval reason when the conversation is awaiting approval and
    the delivery format is `CSV`.
- `INotificationSender` is simulated. It records and logs the manager notification and the
  report email, and emits an event. No SMTP connection is opened.
- Email delivery for a sensitive request is deferred until approval; because approval
  resolution is Sprint 5, the assistant states that the email will be sent after approval.

## 9. API surface

All endpoints require authentication except where noted.

| Method | Path | Purpose |
| --- | --- | --- |
| `POST` | `/api/conversations` | Start a conversation from `{ "utterance": "..." }`; returns a turn. |
| `POST` | `/api/conversations/{id}/answers` | Advance the conversation with a typed answer; returns a turn. |
| `GET` | `/api/conversations/{id}` | Return the current turn to resume after refresh. |
| `GET` | `/api/conversations/{id}/report.csv` | Stream the CSV report when deliverable. |
| `GET` | `/api/governance/approval` | Return `{ "enabled": true }`. |
| `PUT` | `/api/governance/approval` | Set the flag from `{ "enabled": false }`; restricted by role. |

The answers request carries only the field for the current step, for example
`{ "email": "bnayak@enterprise.com" }`, `{ "purpose": "...", "projectCode": "PROJ-1" }`,
`{ "managerEmail": "..." }`, `{ "columns": ["name", "price"] }`, or
`{ "delivery": "CSV" }`. Unknown fields are ignored.

A turn response has this shape:

```json
{
  "conversationId": "c1f2a9",
  "step": "Columns",
  "kind": "ComplexLlmRequired",
  "assistantMessage": "Confirm the columns for your report.",
  "control": "columns",
  "emailPrefill": "bnayak@enterprise.com",
  "columns": [
    { "name": "name", "selected": true },
    { "name": "address.market", "selected": true }
  ],
  "deliveryOptions": ["EMAIL", "CSV"],
  "accessRequestId": null,
  "approvalRequired": false,
  "downloadable": false,
  "result": null,
  "validationError": null
}
```

`control` is one of `none`, `email`, `purpose`, `columns`, `delivery`. `result` carries the
existing NLP response fields for direct answers.

## 10. Data model

- `ReportIntake(RequesterEmail, Purpose, ProjectCode?, ManagerEmail, Columns, DeliveryFormat)`.
- `AccessRequest` gains a trailing optional `ReportIntake? Intake = null`, following the
  repository convention that positional records only gain trailing optional parameters with
  defaults.
- `IAccessRequestStore` gains `UpdateAsync(AccessRequest, CancellationToken)`; the
  in-memory store remains.
- `ConversationState(Id, SessionId, Step, Kind, Mql, AccessRequestId, SensitiveFields, ReportIntake draft, Deliverable, CreatedAt, UpdatedAt)`.
- `IConversationStore` with `CreateAsync`, `GetAsync`, and `UpdateAsync`, implemented
  in-memory.

## 11. Frontend UX

- The main column is a chat thread. User messages are right-aligned bubbles; assistant
  messages are left-aligned. A typing indicator shows while a request is in flight.
- The composer sits at the bottom. The submit control is a button labelled **Send** with
  `aria-label="Send message"`; Enter sends and Shift+Enter inserts a newline; the composer
  is disabled while a request is pending.
- Inline step controls render inside the assistant turn:
  - email: a single-line input with a confirm button;
  - purpose: a text area plus an optional project code input;
  - columns: a checkbox list with a select-all control;
  - delivery: two radio options plus confirm.
- The agent activity stream and SSE status move from the primary panel into a collapsible
  right rail so the chat is the focus.
- The greeting chips become starter prompts that populate the composer.
- A "Governance: approval gate" switch appears in the header only when the user's role
  claim is in the allow-list (`Data Owner / Admin`). It reads the flag on load and writes
  it on change, with an error message if the write is rejected.
- The conversation id is kept in memory and the thread is reloaded through
  `GET /api/conversations/{id}` on refresh.

## 12. Demo report path

- When `ComplexLlmFailed` is returned and the demo path is enabled, the gateway substitutes
  a deterministic demo pipeline derived from the utterance and marks the turn with a
  `demo: true` flag and a visible "Demo report" label in the assistant message. The guided
  intake then proceeds normally.
- The demo path is controlled by `Reports:DemoFallbackEnabled` (default `true`) and is
  disabled in the Production environment.

## 13. Events

New server-sent events published through the existing `IAgentEventSink`, in addition to the
existing `governance.paused`:

| Event | When |
| --- | --- |
| `conversation.started` | A report-producing query starts the intake. |
| `conversation.completed` | The final delivery step is confirmed. |
| `governance.request_enriched` | The access request is stored with the intake. |
| `governance.manager_notified` | The simulated manager notification is recorded. |
| `report.ready` | The report is deliverable as CSV. |
| `report.email_simulated` | The simulated report email is recorded. |

## 14. Testing

- C# unit tests: every step transition and its validation, resume after refresh, sensitive
  versus non-sensitive completion, flag on and off, `GuardrailEvaluator` flag behaviour with
  sensitive fields still reported, access request enrichment, CSV column selection and
  quoting, manager email differing from the requester email.
- API contract tests: `401` on each new endpoint without a token, happy-path start and
  advance, `403` for a non-privileged approval toggle, `200` and `409` from the CSV endpoint.
- Angular tests: the send button label and `aria-label`, rendering of each control type,
  column toggle behaviour, delivery selection, and governance toggle visibility by role.
- The full existing test suite must stay green and the existing benchmarks must be
  unaffected.

## 15. Acceptance criteria

- A complex query opens the chat intake and steps through email, purpose, manager email,
  columns, and delivery.
- A simple lookup answers directly with no intake.
- Invalid answers keep the user on the same step with a clear message.
- With the approval flag on, a sensitive query ends in a `PENDING_LEAD` access request that
  includes the intake, and the assistant explains the approval process.
- With the approval flag off, the same sensitive query completes without approval.
- The approval toggle is visible and usable only for `Data Owner / Admin`, and takes effect
  on the next request without a restart.
- Choosing CSV produces a real in-memory download limited to the confirmed columns;
  choosing email records a simulated send.
- The demo report path lets the full flow run with no LLM key configured and is labelled.
- No emojis, no unresolved placeholders, and no disk writes for exports.

## 16. Non-goals and risks

- **Not real execution.** The report uses demo rows until Sprint 6 wires MongoDB. The demo
  label prevents overclaiming.
- **Not real email.** Delivery and notification are recorded and logged only until Sprint 7.
- **Approval cannot be resolved yet.** The request stays `PENDING_LEAD`; the decision UI is
  Sprint 5. The assistant must say this plainly.
- **Flag misuse risk.** The flag only affects the approval gate; it cannot relax read-only
  or whitelist rules, and the write endpoint is role-restricted and audited through the
  existing event stream.
- **State loss risk.** In-memory conversation and access request stores are cleared on
  restart; this is acceptable for this slice and stated in the UI copy where relevant.

## 17. Main new files

- `src/gateway/Conversations/` — `ConversationStep`, `ConversationState`, `IConversationStore`, `InMemoryConversationStore`, `ConversationOrchestrator`, request and response records.
- `src/gateway/Governance/` — `IApprovalFlagStore`, `InMemoryApprovalFlagStore`.
- `src/gateway/Reports/` — `DemoListingSource`, `CsvExporter`, `INotificationSender`, `SimulatedNotificationSender`.
- `src/gateway/Nlp/Guardrails/` — `AccessRequest` gains `Intake`; `IAccessRequestStore` gains `UpdateAsync`.
- `src/web/src/app/chat/` — chat thread component, step control components, `conversation.service.ts`, and models.
