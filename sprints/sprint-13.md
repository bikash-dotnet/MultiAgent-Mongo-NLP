# Sprint 13 — MCP Server Registry (DB-Backed) and Agent/Server Inventory

**Duration:** 2 weeks
**Sprint goal:** Bring Model Context Protocol (MCP) servers under the same governed configuration plane as schemas and settings: register, enable, secure, test, and inspect MCP servers from the UI, with all definitions stored in MongoDB and all secrets kept out of the database.

**BRD version:** 4.2
**Depends on:** Sprint 11 (settings store + `ConfigController`), Sprint 10 (schema registry)

---

## Traceability

| BRD ID / § | Priority | Coverage in this sprint |
| --- | --- | --- |
| BRD §3.2 | — | MCP becomes a managed backend integration, not a loose local process |
| BRD §4 | High | Agents gain a governed tool surface via allow-listed MCP tools |
| BRD §11 | — | External data/tool sources are declared and audited |
| BRD-NFR-08 | Critical | Only read-only MCP tools can be enabled; write/admin tools are denied |
| BRD-NFR-12 | Critical | MCP secrets stay in environment/secret configuration, never in the DB or UI |
| BRD-NFR-09 | — | Every MCP definition change and tool invocation is audited |

---

## Context

The repository already contains one MCP server, but only in the standalone proof of concept:
`src/poc/mcp-server.js.js` (name `mongo-ai-query` v3.0.0, stdio transport). It exposes four
tools (`get_schema`, `generate_pipeline`, `run_pipeline`, `ask`) and is configured entirely
through environment variables (`src/poc/mongo-ai-query.js:29-42`) and an MCP client's
`mcpServers` block (`src/poc/ReadMe.md:248-263`). It is not referenced by the .NET solution,
the gateway, or the SPA.

This sprint moves MCP from "a POC script an operator edits by hand" to a **registered,
administrable integration** owned by the gateway and driven from the UI. It does not require
deploying the POC server; the POC is used as a reference endpoint for connectivity tests.

### Relation to agents (main project inventory)

The main project has **six logical agents** defined in BRD §4, implemented as collaborating
services rather than an `Agent` base class:

1. Orchestrator Agent (Supervisor) — `NlpOrchestrator`, `ConversationOrchestrator`
2. Hybrid Intent & Query Generator Agent — `NlpRouter`, `IntentClassifier`, `SemanticCache`, `SlotExtractor`, `SemanticKernelLlmQueryGenerator`, `SelfCorrectingLlmQueryGenerator`
3. Schema & Semantic Validator Agent — `PipelineValidator`, `MqlAnalyzer`, `SchemaWhitelist`
4. Guardrail & Security Agent — `GuardrailEvaluator`, `ISensitiveFieldRegistry`, `ReadOnlyRule`
5. Execution Runner Agent — `ExecutionRunner`, `RoutingQueryExecutor`, `MongoQueryTransport`, `EnterpriseCoreQueryTransport`
6. Narrative Insights & Export Delivery Agent — `NarrativeInsights`, `ExportDeliveryService`, `CsvExporter`, `XlsxExporter`, `PdfBriefingRenderer`, `MailKitEmailSender`

`GovernanceService` is cross-cutting (approvals/overrides) and is not one of the six. There is
currently **no first-class agent registry**; agent identity surfaces only through the `agent.*`
SSE event vocabulary. Sprint 13 stores the MCP tool surface these agents may call; a dedicated
Agent Registry (per-agent enablement, model binding, tool grants) is a follow-on sprint.

---

## Domain model

**McpServerDefinition** (`mcp_servers` collection via `IDocumentStore`)

| Field | Type | Notes |
| --- | --- | --- |
| `id` | string | Stable id, e.g. `mcp_mongo_ai_query` |
| `name` | string | Display name |
| `description` | string | Optional |
| `transport` | enum | `Stdio` or `Http` |
| `command` | string? | Stdio executable (e.g. `node`) |
| `args` | string[] | Stdio arguments |
| `url` | string? | HTTP/SSE endpoint when `transport = Http` |
| `envKeys` | string[] | Environment variable **names** only; values resolved from secret configuration |
| `enabled` | boolean | Disabled servers are never started |
| `allowedTools` | string[] | Allow-list; empty means no tools are callable (deny by default) |
| `readOnly` | boolean | Must remain `true`; the server may not expose write/admin tools |
| `timeoutMs` | int | Per-call timeout (default 5,000 ms, aligned with BRD-NFR-03) |
| `scope` | enum | `Global`, `Schema`, `Role` |
| `scopeValue` | string? | Schema id or role when scoped |
| `version` | int | Optimistic concurrency |
| `updatedAt` / `updatedBy` | — | Audit metadata |

Secrets are **never** stored in this document. API keys and credentials are referenced by name
via `envKeys` and resolved at process start from environment/secret providers, consistent with
Sprint 11's secret policy.

---

## Stories

### S13-01 Domain model, store, and seed
- Implement `McpServerDefinition` and `IMcpServerRegistry` over `IDocumentStore`
- Seed (disabled by default) an entry for the local POC endpoint `mongo-ai-query` using
  `transport = Stdio`, `command = node`, `args = ["<repo>/src/poc/mcp-server.js.js"]`,
  `envKeys = ["MONGO_URI", "API_KEY"]`, and the four known tools in `allowedTools`
- Idempotent seeding; never overwrite an operator-edited definition
- Document the POC reference entry as sample data that can be removed

### S13-02 MCP registry API (`/api/mcp-servers`)
- `GET /api/mcp-servers` — list summaries (id, name, transport, enabled, health, tool count)
- `GET /api/mcp-servers/{id}` — full definition, secret values redacted
- `POST /api/mcp-servers` / `PUT /api/mcp-servers/{id}` — create/replace with validation (`version` concurrency)
- `DELETE /api/mcp-servers/{id}`
- `POST /api/mcp-servers/{id}/enable` and `/disable`
- `POST /api/mcp-servers/{id}/test` — connect, initialize, list tools, return health and latency
- `GET /api/mcp-servers/{id}/tools` — discovered tools with name, description, input schema, and allow-list status
- All mutating endpoints require `Data Owner / Admin`; reads require authentication
- Reject definitions that reference a write/admin tool unless explicitly impossible to classify

### S13-03 MCP host and connectivity
- Implement `IMcpHost` using the official .NET MCP client to open `Stdio` child processes or
  `Http` transports, perform the initialize handshake, and enumerate tools
- Connection lifecycle: lazy start on first use, per-server timeout, exponential backoff on
  failure, and a circuit breaker so a dead server never blocks the query path
- Health surfaced to the registry (`Healthy`, `Degraded`, `Down`, `Disabled`) with last error and
  last-checked timestamp
- Never log secret values; log only server id, transport, and redacted command/args
- Expose discovered tools to the agent tool surface (Semantic Kernel function space) behind the
  allow-list; **tool invocation from the live query pipeline is out of scope this sprint**

### S13-04 MCP Servers UI
- `/mcp-servers` route (also linked from the Configuration console): `p-table` of servers with
  enabled `p-inputSwitch`, transport tag, health badge, and tool count
- Editor dialog: name, description, transport `p-select`, command/args, URL, `envKeys` (`p-chips`),
  `allowedTools` (`p-multiselect`, populated from the Test connection result), timeout, scope
- "Test connection" button showing discovered tools, latency, and errors; per-tool allow/deny toggles
- Secret fields are never shown; `envKeys` render as "managed by environment" with a
  configured/not-configured indicator only
- Delete and disable confirmations; changes reflect immediately

### S13-05 Security, RBAC, and audit
- Deny-by-default tool allow-list; only read-only tools may be enabled (BRD-NFR-08)
- Validate stdio commands against an allow-list of executables (`node`, `dotnet`, `npx`) and
  reject shell metacharacters/argument injection; document the residual trust boundary
- Validate HTTP URLs (scheme + host allow-list) to prevent arbitrary reach-out
- Append an audit record on create/update/enable/disable/delete and on each tool invocation
  (server id, tool, requester, outcome, duration) — never mutate history (BRD-NFR-09)
- Add tests asserting no secret value appears in any `/api/mcp-servers` response

### S13-06 POC documentation hygiene
- Reconcile the POC doc with its code: `src/poc/ReadMe.md:338-341` claims `GEMINI_API_KEY` /
  `GEMINI_MODEL` aliases, but `src/poc/mongo-ai-query.js:37-38` reads only `API_KEY` / `MODEL`
- Either implement the `GEMINI_*` aliases in code or correct the README to match; note the
  doubled `.js` filename (`mcp-server.js.js`) as intentional per `src/poc/ReadMe.md:69-70`
- Add a short "not part of the production solution" banner to `src/poc/ReadMe.md`

### S13-07 Tests
- Store tests: seed idempotency, version concurrency, enable/disable
- API tests: CRUD, validation, authorization, secret redaction
- Host tests: initialize + list tools against a stub stdio server; timeout, backoff, circuit breaker
- Security tests: shell-metacharacter rejection, URL allow-list, write-tool denial
- Web tests: list rendering, editor validation, test-connection flow, allow-list toggles

---

## Acceptance criteria

- [ ] MCP servers can be registered, viewed, edited, enabled/disabled, tested, and deleted via `/api/mcp-servers` and the `/mcp-servers` UI
- [ ] Definitions persist in MongoDB through `IDocumentStore`; the POC server is seeded disabled and can be removed
- [ ] A Test connection action performs the MCP handshake and lists tools with a health/latency result
- [ ] Only allow-listed **read-only** tools can be enabled; write/admin tools are rejected (BRD-NFR-08)
- [ ] No secret value is stored in `mcp_servers`, returned by the API, or rendered in the UI (BRD-NFR-12)
- [ ] stdio commands and HTTP URLs are validated against allow-lists; argument injection is rejected
- [ ] Changes and tool invocations are audited; mutations are restricted to `Data Owner / Admin`
- [ ] The POC README matches its code and is marked as non-production
- [ ] `dotnet test tests/Gateway.Tests` and `npm test` pass

---

## Out of scope (platform, per BRD §14)

- Invoking MCP tools from the live NLP/query pipeline (follow-on sprint; this sprint delivers the registry, connectivity, and tool discovery)
- A dedicated Agent Registry (per-agent model/tool grants); agent inventory is documented in Context only
- Deploying or bundling third-party MCP servers; operators register them explicitly
- Storing secrets in the database (they remain environment/secret-manager concerns)
- Modifying the POC MCP server's behavior beyond documentation

---

## Exit artifacts

- `src/gateway/Mcp/*` — `McpServerDefinition`, `IMcpServerRegistry`, store, host/client, validators, secret projection
- `/api/mcp-servers` endpoints wired in `Program.cs`
- `src/web/src/app/mcp-servers/*` registry UI with test-connection and tool allow-list editing
- `src/poc/ReadMe.md` corrected and marked non-production
- Gateway + web tests covering CRUD, connectivity, security, secret redaction, and audit
