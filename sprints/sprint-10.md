# Sprint 10 — Schema Registry Management (Add / View / Activate Sources)

**Duration:** 2 weeks
**Sprint goal:** Move the schema contract out of static files into a MongoDB-backed registry that can be created, viewed, edited, and activated from the UI, and make the guardrail, validator, and column catalog resolve from the active schema.

**BRD version:** 4.2
**Depends on:** Sprint 9

---

## Traceability

| BRD ID / § | Priority | Coverage in this sprint |
| --- | --- | --- |
| BRD §5.1 | High | Field sensitivity registry (`schema_field_registry`) becomes editable and durable |
| BRD §6.3 | High | `schema_field_registry` logical contract implemented as stored documents, plus schema metadata |
| BRD §12 | — | `schema.txt` whitelist and related assets resolve from the active schema |
| BRD-FR-05 | Critical | Flag verification reads the active schema instead of `field_registry.json` |
| BRD-NFR-08 | Critical | Unknown-field rejection uses the active schema whitelist |
| BRD-NFR-09 | — | Schema changes are audited; registry history is append-only |

---

## Current state (baseline)

- `SchemaWhitelist.LoadFromSchemaFile(.../Prompts/schema.txt)` — file-backed whitelist.
- `InMemorySensitiveFieldRegistry.LoadFromDirectory(.../Schema/field_registry.json)` — file-backed flags.
- `ColumnCatalog.Standard` — hardcoded result columns.
- All three are registered as singletons in `NlpServiceCollectionExtensions.cs` and read once at startup.

---

## Domain model

**SchemaDefinition** (`schemas` collection via `IDocumentStore`)

| Field | Type | Notes |
| --- | --- | --- |
| `id` | string | Stable id, e.g. `sch_airbnb_listings` |
| `name` | string | Display name |
| `description` | string | Optional |
| `dataSource` | enum | `Mongo` or `EnterpriseCoreREST` (mirrors `ExecutionDataSource`) |
| `database` | string | Target database |
| `collection` | string | Target collection |
| `fields` | SchemaField[] | Inline field contracts (or a separate collection) |
| `isActiveSource` | boolean | Exactly one active source; enforced server-side |
| `version` | int | Incremented on each saved edit |
| `createdAt` / `updatedAt` / `updatedBy` | — | Audit metadata |

**SchemaField**

| Field | Type | Notes |
| --- | --- | --- |
| `path` | string | Dot-path, e.g. `address.location.coordinates` |
| `dataType` | enum | `string` / `number` / `boolean` / `date` / `object` / `array` |
| `isSensitive` | boolean | BRD §5.1 |
| `requiresApproval` | boolean | BRD §5.1 |
| `dataOwnerRoles` | string[] | BRD §5.1 |
| `description` | string | Optional, shown as a hint in the editor |

---

## Stories

### S10-01 Schema domain model and store
- Implement `SchemaDefinition`, `SchemaField`, and `ISchemaRegistry` backed by `IDocumentStore`
  (durable via `SchemaRegistryStore`, file fallback included by the existing store seam)
- Idempotent seed on first run: import `field_registry.json` (flags/roles) and `schema.txt` (field
  paths) into one default `sch_airbnb_listings` schema, marked active, using `sample_airbnb`
  / `listingsAndReviews` as source metadata
- Startup must not fail when the store already contains schemas

### S10-02 Schema management API (`/api/schemas`)
- `GET /api/schemas` — list summaries (id, name, dataSource, field count, isActiveSource, version)
- `GET /api/schemas/{id}` — full schema with fields
- `POST /api/schemas` — create (name, dataSource, database, collection, fields)
- `PUT /api/schemas/{id}` — replace metadata and fields; bump `version`
- `POST /api/schemas/{id}/fields` — add a field; `PUT`/`DELETE` on `/fields/{path}` for edits/removal
- `POST /api/schemas/{id}/activate` — set as the single active source
- `POST /api/schemas/import` — bulk import fields from JSON or pasted `schema.txt` lines
- All mutating endpoints require `Data Owner / Admin`; reads require authentication

### S10-03 Active-source runtime resolution
- Introduce `IActiveSchemaProvider` that returns the active `SchemaDefinition` and invalidates on change
- Replace the file-backed `SchemaWhitelist` and `ISensitiveFieldRegistry` singletons with providers
  that build from the active schema; keep the existing interfaces so `GuardrailEvaluator` is untouched
- Replace `ColumnCatalog.Standard` with columns derived from the active schema fields (falling back
  to the current standard set when the schema has none)
- Resolve the validation `schema.txt` prompt asset from the active schema (rendered field list)
- Cache the compiled whitelist/registry and invalidate on activate/update (no per-query file reads)

### S10-04 Schema registry UI
- `/schemas` route: `p-table` of schemas with an "Active source" tag and Set-as-source action
- Schema editor: metadata form (`p-inputText`, `p-select` for data source) plus an editable field
  grid (`p-table` with row editing or a `p-dialog` per field)
- Field editor fields: path, data type, isSensitive, requiresApproval, dataOwnerRoles (`p-multiselect`),
  description; client-side validation for required and duplicate paths
- Import dialog: paste `schema.txt` lines or upload JSON, preview parsed fields before commit
- Clear confirmation when activating a source and when deleting a field

### S10-05 Validation, versioning, and audit
- Server-side validation: non-empty path, unique paths, valid enum values, at least one active source
- On every create/update/activate, append an audit record (`audit_logs` collection) capturing actor,
  schema id, action, and version — never mutate prior history (BRD-NFR-09)
- Optimistic concurrency: reject `PUT` when the submitted `version` is stale

### S10-06 Tests
- Store tests: seed idempotency, activate exclusivity, version-increment, concurrency rejection
- Guardrail integration: a field flagged in the active schema triggers `governance.paused`; an
  unknown path is rejected (BRD-NFR-08)
- Catalog test: activating a schema changes available result columns
- Web tests: schema list, editor validation, activate flow

---

## Acceptance criteria

- [ ] Schemas can be created, viewed, edited, deleted, and listed via the API and the `/schemas` UI
- [ ] Exactly one schema is the active source at any time; activating another flips it atomically
- [ ] A schema can be seeded from the existing `field_registry.json` + `schema.txt` and then edited without code changes
- [ ] Guardrail flagging and unknown-field rejection read from the active schema, not the files
- [ ] Result columns offered to the chat flow derive from the active schema
- [ ] Schema changes are versioned, concurrency-checked, and appended to the audit trail
- [ ] Mutating endpoints reject non-`Data Owner / Admin` callers
- [ ] `dotnet test tests/Gateway.Tests` and `npm test` pass

---

## Out of scope (platform, per BRD §14)

- Automatic introspection of a live MongoDB/Enterprise source to generate fields (import is manual or pasted)
- Write/mutate access to business collections
- Business rules and per-column display policy (Sprint 12)
- Runtime configuration values such as timeouts and thresholds (Sprint 11)

---

## Exit artifacts

- `src/gateway/Schemas/*` domain, store, provider, and validators
- `/api/schemas` endpoints wired in `Program.cs`
- Reworked `SchemaWhitelist` / `ISensitiveFieldRegistry` / `IColumnCatalog` resolution from the active schema
- `src/web/src/app/schemas/*` registry and editor UI
- Gateway + web tests for the registry and guardrail integration
