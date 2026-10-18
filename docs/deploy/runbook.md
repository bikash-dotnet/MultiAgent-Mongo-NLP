# Deployment Runbook — Runtime Configuration Console (Sprint 11)

## Environment variable / secret matrix

All values can be provided via `appsettings.*.json` or environment variables using the `:` section separator (or `__` on platforms that do not support `:` in env names).

| Section | Key | Env variable | Default | Secret? | Notes |
|---------|-----|--------------|---------|---------|-------|
| `Jwt` | `Issuer` | `Jwt:Issuer` | `MultiAgentMongoNlp` | No | JWT issuer |
| `Jwt` | `Audience` | `Jwt:Audience` | `MultiAgentMongoNlp.Spa` | No | JWT audience |
| `Jwt` | `SigningKey` | `Jwt:SigningKey` | `DEV-ONLY-CHANGE-ME-32CHARS-MINIMUM!!` | **Yes** | Must be ≥ 32 chars; rotate in production |
| `MongoDb` | `ConnectionString` | `MongoDb:ConnectionString` | `mongodb://localhost:27017` | **Yes** | May contain credentials |
| `MongoDb` | `Database` | `MongoDb:Database` | `sample_airbnb` | No | |
| `Execution` | `DataSource` | `Execution:DataSource` | `Mongo` | No | `Mongo` or `EnterpriseCoreREST` |
| `Execution` | `TimeoutMs` | `Execution:TimeoutMs` | `5000` | No | Range 1000–60000 |
| `Execution` | `Collection` | `Execution:Collection` | `listingsAndReviews` | No | |
| `Execution` | `Database` | `Execution:Database` | `sample_airbnb` | No | |
| `EnterpriseCore` | `BaseUrl` | `EnterpriseCore:BaseUrl` | `""` | No | Required when `Execution:DataSource=EnterpriseCoreREST` |
| `EnterpriseCore` | `QueryPath` | `EnterpriseCore:QueryPath` | `/api/query` | No | |
| `Persistence` | `Mode` | `Persistence:Mode` | `auto` | No | `file`, `mongodb`, or `auto` |
| `Persistence` | `DataDirectory` | `Persistence:DataDirectory` | `.data` | No | Used by file store |
| `NvidiaNim` | `BaseUrl` | `NvidiaNim:BaseUrl` | `https://integrate.api.nvidia.com/v1` | No | |
| `NvidiaNim` | `ApiKey` | `NvidiaNim:ApiKey` | `TBD` | **Yes** | NVIDIA NIM API key |
| `NvidiaNim` | `Model` | `NvidiaNim:Model` | `nvidia/nemotron-3-nano-omni-30b-a3b-reasoning` | No | |
| `NvidiaNim` | `MaxAttempts` | `NvidiaNim:MaxAttempts` | `3` | No | Range 1–10 |
| `NvidiaNim` | `TimeoutSeconds` | `NvidiaNim:TimeoutSeconds` | `30` | No | Range 5–300 |
| `Nlp:Embeddings` | `ModelPath` | `Nlp:Embeddings:ModelPath` | `Models/bge-small-en-v1.5/model_quantized.onnx` | No | |
| `Nlp:Embeddings` | `TokenizerPath` | `Nlp:Embeddings:TokenizerPath` | `Models/bge-small-en-v1.5/vocab.txt` | No | |
| `Nlp:Embeddings` | `MaxTokens` | `Nlp:Embeddings:MaxTokens` | `512` | No | Range 64–4096 |
| `Nlp:Cache` | `Threshold` | `Nlp:Cache:Threshold` | `0.95` | No | Range 0.0–1.0 |
| `Nlp:Defaults` | `Limit` | `Nlp:Defaults:Limit` | `10` | No | Range 1–1000 |
| `Nlp:Defaults` | `Sort` | `Nlp:Defaults:Sort` | `rating_desc` | No | `rating_desc`, `price_desc`, `name_asc` |
| `Nlp:Defaults` | `Market` | `Nlp:Defaults:Market` | `All` | No | `All`, `US`, `EU`, `APAC` |
| `Reports` | `DemoFallbackEnabled` | `Reports:DemoFallbackEnabled` | `true` | No | Production override: `false` |
| `Governance` | `ApprovalEnabled` | `Governance:ApprovalEnabled` | `true` | No | DB-backed; editable in console |

## Secret safety

- Secrets are stored in the `app_settings` collection (DB-backed runtime config) but are **masked as `null`** in all API responses (`GET /api/config`, `GET /api/config/{section}`).
- The Angular console renders secrets as `Configured` / `Not configured` badges only; the actual value is never exposed to the browser.
- `Jwt:SigningKey`, `MongoDb:ConnectionString`, and `NvidiaNim:ApiKey` are treated as secrets. Do not commit real values to source control; use `appsettings.Development.json` for local dev and environment variables or a secret store for production.

## Runtime configuration flow

1. Bootstrap defaults come from `appsettings.*.json` bound to `IOptions<T>` at startup.
2. DB-backed overrides live in the `app_settings` collection and take precedence over bootstrap defaults.
3. `PUT /api/config/{section}` validates ranges/cross-field rules, persists with optimistic concurrency (version), applies hot-reload via `ISettingsProvider`, and writes an audit log (`SchemaAction = "config.update"`).
4. `ExecutionRunner` (timeout) and `SemanticCache` (threshold) read hot values from `ISettingsProvider` at execution time — no restart required.
5. `DbApprovalFlagStore` backs the governance approval flag from the `Governance` section; falls back to `InMemoryApprovalFlagStore` when the settings store is unavailable.

## Verification

- `dotnet test` — 292 tests pass (includes `ApiConfigTests`: auth, schema, masked secrets, update, stale-version conflict, range validation, cross-field rule, delete/restore, reset).
- `npm test` (Angular) — 42 tests pass (includes `configuration.component.spec.ts`: rendering, save, reset, non-admin gating, error handling).
- `npm run build -- --configuration production` — clean.
