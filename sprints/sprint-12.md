# Sprint 12 — Business Rules and Column Definitions

**Duration:** 2 weeks
**Sprint goal:** Make business rules and result/column definitions first-class, DB-backed configuration editable from the UI, and apply them consistently across guardrails, execution, the results grid, report columns, and exports.

**BRD version:** 4.2
**Depends on:** Sprint 10 (schemas) and Sprint 11 (settings store)

---

## Traceability

| BRD ID / § | Priority | Coverage in this sprint |
| --- | --- | --- |
| BRD §5.2 / §5.3 | High | Role and override policies expressed as ordered, editable rules |
| BRD-FR-05 | Critical | Sensitive-field gating respects rule-driven approval overrides |
| BRD-FR-06 | High | Data-owner exemption and role behavior become data, not code |
| BRD-NFR-07 | — | Clarification/default rules stay bounded per turn |
| BRD-NFR-08 | Critical | Read-only, row-cap, and masking policies enforced from the rule set |
| BRD §9.3 | — | Result presentation (columns, labels, formats, visibility) is config-driven |
| BRD-NFR-09 | — | Rule and column changes are audited |

---

## Concepts

- **Business rule:** an ordered, scoped, enable-able policy with a condition and an action.
- **Column definition:** metadata for a result field (label, data type, format, visibility,
  exportability, order, width) used by the grid, the report intake column picker, and exports.

Both live in the durable store and are edited from the UI. Both are validated server-side.

---

## Domain model

**BusinessRule** (`business_rules` collection via `IDocumentStore`)

| Field | Type | Notes |
| --- | --- | --- |
| `id` | string | Stable id |
| `name` | string | Display name |
| `description` | string | Optional |
| `scope` | enum | `Global`, `Schema`, `Role` |
| `scopeValue` | string? | Schema id or role when scoped |
| `priority` | int | Lower runs first; deterministic tie-break by id |
| `enabled` | boolean | Disabled rules are skipped |
| `condition` | object | Field / operator / values (structured, no code) |
| `action` | object | Type + parameters (structured) |
| `version` | int | Optimistic concurrency |
| `updatedAt` / `updatedBy` | — | Audit metadata |

**Supported conditions (v1):** `field` (dot-path), `operator` in
`equals | notEquals | in | notIn | contains | greaterThan | lessThan | matchesDataOwnerRole`,
value(s). Conditions combine with `all` (AND) or `any` (OR).

**Supported actions (v1):**

| Action | Parameters | Effect |
| --- | --- | --- |
| `setDefault` | key, value | Apply a default (limit, sort, market) when the user did not specify |
| `capRows` | max | Cap rows returned/exported |
| `maskColumn` | field | Mask values unless the requester is exempt |
| `excludeColumn` | field | Remove a field from results/exports |
| `requireApproval` | field | Force the approval gate for a field |
| `exemptRole` | role | Exempt a role from gating for matching fields |
| `denyOperator` | operator | Reject write/admin operators (defense in depth) |

**ColumnDefinition** (`column_definitions` collection via `IDocumentStore`)

| Field | Type | Notes |
| --- | --- | --- |
| `id` / `field` | string | Dot-path key |
| `label` | string | Display header |
| `dataType` | enum | `string` / `number` / `currency` / `boolean` / `date` |
| `format` | string? | e.g. currency code, date pattern |
| `order` | int | Grid/export order |
| `visibleByDefault` | boolean | Initial grid visibility |
| `exportable` | boolean | Included in CSV/XLSX/PDF |
| `sensitive` | boolean | Masked in UI unless exempt (feeds from schema) |
| `width` | string? | Optional grid hint |

---

## Stories

### S12-01 Domain model, store, and seed
- Implement `BusinessRule` and `ColumnDefinition` with `IBusinessRuleStore` / `IColumnDefinitionStore`
  over `IDocumentStore`
- Seed sensible defaults on first run: `capRows` from the current default limit, `denyOperator` safety
  rules mirroring BRD-NFR-08 operators, and column definitions derived from the active schema's standard
  columns (the existing `ColumnCatalog.Standard` set)
- Idempotent seeding; no startup failure on existing data

### S12-02 Rule engine integration
- Implement `BusinessRuleEngine` compiling enabled rules in priority then id order, scoped to the
  requesting role and the active schema
- Apply in the pipeline:
  - `ConversationOrchestrator` / `NlpOrchestrator` — `setDefault` values before query synthesis
  - `GuardrailEvaluator` — `requireApproval`, `exemptRole`, `denyOperator` layered on schema flags
  - `ExecutionRunner` — `capRows` on result sets
  - Export/report path — `excludeColumn`, `exportable`, `capRows` honored for CSV/XLSX/PDF
  - Result shaping — `maskColumn` for non-exempt requesters
- Rules cannot weaken BRD-NFR-08: read-only enforcement remains terminal regardless of rule configuration
- Record `ruleId`(s) applied and any masking/capping in the audit document

### S12-03 Column definitions API and catalog integration
- `GET/POST/PUT/DELETE /api/column-definitions`; `POST /api/column-definitions/reorder`
- `ColumnCatalog` (`IColumnCatalog`) returns definitions (label, type, order, visibility, exportable)
  instead of bare names, falling back to safe defaults for fields without a definition
- Result grid, report intake column picker, and exporters consume the same definitions
- Validate `dataType`/`format` pairs; unknown fields warn but do not fail

### S12-04 Business rules UI
- `/business-rules` route: `p-table` of rules with enabled toggle, priority, scope, and applied-to tags
- Rule editor (`p-dialog`): general fields, structured condition builder (field `p-select`, operator
  `p-select`, values `p-chips`/`p-select`), and action picker with parameter controls
- Enable/disable inline; duplicate; reorder (drag or priority input); delete with confirmation
- Rule tester: enter a sample requester role + field set and preview which rules would fire and the
  resulting effect

### S12-05 Column definitions UI
- Section on the Business Rules route (or its own tab): ordered `p-table` with inline edit of label,
  data type, format, visibility, exportability, and order
- Reorder controls, "preview columns" panel, and a seed/import action from the active schema
- Sensitive columns show a lock indicator and inherit masking behavior from the rule set

### S12-06 RBAC, audit, validation, and tests
- Reads require authentication; writes require `Data Owner / Admin`
- Append audit records on rule/column changes (actor, entity, action, version); never mutate history
- Optimistic concurrency via `version`; reject stale writes
- Tests: engine ordering/scoping, each action's effect on guardrail/execution/export, column-driven grid
  and export, masking for non-exempt roles, validation and concurrency, web rule-builder and
  column-editor behavior

---

## Acceptance criteria

- [ ] Business rules can be created, edited, enabled/disabled, reordered, and deleted via the API and `/business-rules` UI
- [ ] Column definitions can be created/edited/reordered and shown in the grid, column picker, and exports
- [ ] `setDefault`, `capRows`, `maskColumn`, `excludeColumn`, `requireApproval`, `exemptRole`, and `denyOperator` take effect end to end
- [ ] Read-only enforcement (BRD-NFR-08) cannot be disabled by any rule
- [ ] Masked columns are hidden from non-exempt requesters in both the grid and exports
- [ ] Rule/column changes are versioned, concurrency-checked, audited, and restricted to `Data Owner / Admin`
- [ ] Rules and column definitions seed from the active schema and existing defaults without code changes
- [ ] `dotnet test tests/Gateway.Tests` and `npm test` pass

---

## Out of scope (platform, per BRD §14)

- A general-purpose scripting/expression language or user-supplied code execution
- Writing to business collections or altering BRD-NFR-08 semantics
- Per-user saved view layouts (only shared column definitions are in scope)
- Machine-learned rule suggestions

---

## Exit artifacts

- `src/gateway/Rules/*` rule and column domain, stores, engine, and validators
- `/api/business-rules` and `/api/column-definitions` endpoints wired in `Program.cs`
- Rule-aware `GuardrailEvaluator`, orchestrators, `ExecutionRunner`, exports, and `ColumnCatalog`
- `src/web/src/app/business-rules/*` rule builder, tester, and column-definition editor
- Gateway + web tests covering engine behavior, masking/capping, column-driven presentation, and audit
