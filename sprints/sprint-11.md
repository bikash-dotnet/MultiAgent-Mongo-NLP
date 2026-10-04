# Sprint 11 — Runtime Configuration Console (ConfigController + DB-Backed Settings)

**Duration:** 2 weeks
**Sprint goal:** Introduce a configuration API and a UI console that move non-secret runtime settings from `appsettings.json` into the durable store, apply them without a restart, and keep secrets and environment-bound infrastructure values out of the database and UI.

**BRD version:** 4.2
**Depends on:** Sprint 9 (shell)

---

## Traceability

| BRD ID / § | Priority | Coverage in this sprint |
| --- | --- | --- |
| BRD §3.2 | — | Backend configuration becomes a governed, observable concern |
| BRD-NFR-03 | — | Execution timeout (default 5,000 ms) is editable and enforced from the store |
| BRD-NFR-05 | — | Semantic cache threshold is editable from the console |
| BRD §11 | — | Data source selection reflected in the console |
| BRD-NFR-12 | Critical | Signing/connection/API secrets stay in environment/secret configuration only |
| BRD-NFR-09 | — | Every settings change is audited |

---

## Principles

1. **Secrets never persist.** `Jwt:SigningKey`, `MongoDb:ConnectionString`, `NvidiaNim:ApiKey`, and
   `Reports:Smtp:Password` are read only from environment variables / secret stores. They are never
   written to the store, never returned by the API, and never entered in the UI.
2. **DB overrides appsettings.** Effective value = DB value when present, otherwise the `appsettings`
   bootstrap default. Deleting a DB override restores the bootstrap default.
3. **Validate then apply.** Every section is validated before it is persisted; invalid payloads are
   rejected with field-level errors and leave the running configuration untouched.
4. **Admin only.** Reads require authentication; writes require `Data Owner / Admin`.

---

## Editable vs environment-bound

| Section | Keys | Treatment |
| --- | --- | --- |
| `Execution` | `DataSource`, `TimeoutMs`, `Collection`, `Database` | Editable (DB) |
| `Nlp:Cache` | `Threshold` | Editable (DB) |
| `Nlp:Defaults` | `Limit`, `Sort`, `Market` | Editable (DB) |
| `Nlp:Embeddings` | `MaxTokens` | Editable (DB); model/tokenizer paths read-only |
| `Governance` | `ApprovalEnabled` | Editable (DB); unifies with the existing approval flag store |
| `Reports` | `DemoFallbackEnabled` | Editable (DB) |
| `EnterpriseCore` | `BaseUrl`, `QueryPath` | Editable (DB, admin-only; validate scheme/host) |
| `NvidiaNim` | `Model`, `BaseUrl`, `MaxAttempts`, `TimeoutSeconds` | Editable (DB) |
| `NvidiaNim` | `ApiKey` | Environment/secret — shown as "configured: yes/no" only |
| `MongoDb` | `ConnectionString`, `Database` | Environment/secret — not editable |
| `Jwt` | `Issuer`, `Audience`, `SigningKey` | Environment-bound — not editable |
| `Persistence` | `Mode`, `DataDirectory` | Startup-bound — read-only in the UI |
| `Reports:Smtp` | `Host`, `Port`, `User`, `From` | Editable (DB); `Password` secret |

---

## Domain model

**SettingsDocument** (`app_settings` collection via `IDocumentStore`)

| Field | Type | Notes |
| --- | --- | --- |
| `id` | string | Section key, e.g. `Execution` or `Nlp.Cache` (flat, one document per section) |
| `values` | object | Section payload matching the options class shape |
| `version` | int | Incremented per write; optimistic concurrency |
| `updatedAt` / `updatedBy` | — | Audit metadata |
| `source` | enum | `Database` (only DB overrides are stored; absence means bootstrap default) |

---

## Stories

### S11-01 Settings model, store, and seed
- Implement `ISettingsStore` over `IDocumentStore`, one document per section
- Reuse existing options classes (`ExecutionOptions`, `NlpOptions`, `GovernanceOptions`,
  `ReportsOptions`, `EnterpriseCoreOptions`, `NvidiaNimOptions`) as the payload contracts
- No seeding required: a missing document means "use the appsettings/bootstrap default"
- Expose a merged view (bootstrap default + DB override) with a per-key source tag

### S11-02 Configuration API (`ConfigController`, `/api/config`)
- `GET /api/config` — grouped sections with effective values, per-key source (`Database`/`Default`),
  and secret indicators; secret values are never serialized
- `GET /api/config/{section}` — one section
- `PUT /api/config/{section}` — validate, persist override, bump version, hot-apply
- `DELETE /api/config/{section}` — remove override, restore bootstrap default
- `POST /api/config/reset` — clear all overrides (admin, confirmed)
- `GET /api/config/schema` — metadata describing editable keys, types, ranges, and which are read-only/secret, so the UI renders generically
- Whitelist sections and keys server-side; reject unknown keys

### S11-03 Runtime binding and hot reload
- Introduce `ISettingsProvider` that resolves values from store-or-default and raises a change token
- Bind options via `IOptionsMonitor` (or a settings-aware factory) so the running app picks up changes
  without a restart: `ExecutionRunner` timeout (BRD-NFR-03), `SemanticCache` threshold (BRD-NFR-05),
  NLP defaults, `Governance` approval flag, `Reports` demo fallback, `EnterpriseCore` client base URL,
  `NvidiaNim` model/attempts/timeout
- Unify `IApprovalFlagStore` with the `Governance` section so the topbar toggle and the console agree
- Validate on apply; on failure keep the previous effective configuration and return errors
- Environment-bound values (JWT, Mongo URI, API key, SMTP password) continue to come from configuration
  providers and are read once/invalidated as today

### S11-04 Configuration console UI
- `/configuration` route: `p-tabs` or grouped `p-card` panels per section
- Controls: `p-inputNumber` (timeout, ports, attempts, thresholds, limits), `p-select` (data source,
  sort, market), `p-inputSwitch` (booleans), `p-inputText` (URLs, collections, databases)
- Each field shows its source badge (`DB override` / `Default`) and a per-section Reset action
- Inline validation for ranges (timeout <= 60,000 ms; threshold between 0 and 1; port 1–65535) and URLs
- Secret and environment-bound fields render read-only with a "managed by environment" note and a
  configured/not-configured indicator (never the value)
- Save shows a success toast; failures show field errors returned by the API

### S11-05 Secret and environment safety
- Allow-list serialization: the API projects only editable keys; secret keys are replaced with
  `{ configured: boolean }`
- Never log secret values; configuration-change audit records list changed keys, not values of secrets
- Add a test asserting no secret value (signing key, connection string, API key, SMTP password)
  appears in any `/api/config` response
- Document the environment variables and the deploy `.env` placeholders in `docs/deploy/runbook.md`

### S11-06 Validation, audit, and tests
- Per-section validators with clear messages; cross-field checks where needed (e.g. `BaseUrl` required
  when `Execution:DataSource = EnterpriseCoreREST`)
- Append an audit record per change: actor, section, changed keys, previous/next non-secret values, version
- Tests: store merge/source resolution, override/restore, optimistic concurrency, validation rejection,
  hot-apply of timeout and cache threshold, secret non-exposure, web console rendering and validation

---

## Acceptance criteria

- [ ] Non-secret settings in the table above are editable from `/configuration` and persist in the store
- [ ] Effective config is DB override over bootstrap default; deleting an override restores the default
- [ ] Changing execution timeout and cache threshold takes effect without a restart (BRD-NFR-03/NFR-05)
- [ ] JWT signing key, MongoDB URI, NVIDIA API key, and SMTP password never appear in the DB, API responses, or UI
- [ ] The console shows source badges, validates input, and blocks unknown keys
- [ ] Only `Data Owner / Admin` can mutate configuration; all changes are audited
- [ ] `dotnet test tests/Gateway.Tests` and `npm test` pass

---

## Out of scope (platform, per BRD §14)

- Storing or rotating secrets in the database or UI (they remain environment/secret-manager concerns)
- Feature flags beyond the governance approval toggle
- Schema/field contracts (Sprint 10) and business rules/column policy (Sprint 12)
- Kubernetes/cloud config providers

---

## Exit artifacts

- `src/gateway/Configuration/*` settings store, `ISettingsProvider`, validators, secret projection
- `ConfigController` / `/api/config` endpoints wired in `Program.cs`
- Hot-apply wiring across `ExecutionRunner`, `SemanticCache`, NLP defaults, governance, reports, and LLM options
- `src/web/src/app/configuration/*` console UI
- `docs/deploy/runbook.md` updated with the environment-variable/secret matrix
- Gateway + web tests covering merge, validation, hot reload, and secret non-exposure
