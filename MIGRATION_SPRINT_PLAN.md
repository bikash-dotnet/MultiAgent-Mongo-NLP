# Migration Sprint Plan — MongoDB Native Driver + Convention Alignment

## Status (branch `261019-refactor-mongodb-native-driver`)

**Implemented**
- `MongoDocumentStore` reads and writes typed `IMongoCollection<T>` documents with native
  `FindAsync` / `ReplaceOneAsync`; the System.Text.Json round-trip is gone.
- `MongoBsonConfiguration` registers camelCase element names, string enum values, ISO-8601
  `DateTimeOffset` values, and `Id` → `_id` class maps (plus `AgentState.SessionId` and
  `ActivationLock.Id`). JSON string converters remain on enums for HTTP contracts.
- `seed.js` no longer writes a duplicate `id` field; `scripts/migrate-remove-duplicate-id.js`
  cleans legacy documents.
- `RuleSeedHostedService` is retained as a safety net and still skips when data exists.

**Pending**
- Run `scripts/migrate-remove-duplicate-id.js` against each deployed database (no MongoDB
  instance is available in this environment).

## Deferred from current sprint (Option A applied now)

### 1. Native MongoDB Driver Migration (`MongoDocumentStore`)
**File:** `src/gateway/Persistence/MongoDocumentStore.cs`

**Current:** System.Text.Json round-trip (C# → JSON → BsonDocument → MongoDB)
**Target:** `IMongoCollection<T>` with `BsonClassMap` direct serialization

**Changes:**
- Replace `IMongoCollection<BsonDocument>` with `IMongoCollection<T>` per document type
- Remove `BsonDocument.Parse(JsonSerializer.Serialize(...))` round-trip
- Use driver's native `ReplaceOneAsync`/`FindAsync` with typed filters
- Register BSON conventions and class maps (`Id` → `_id`, camelCase elements, string enums)

**Benefits:**
- Eliminates JSON serialization overhead on every read/write
- String enum values are handled natively in BSON (JSON converters remain for HTTP)
- Map C# `Id` → MongoDB `_id` natively (eliminates `_id`/`Id` duplication)

### 2. Deduplicate `_id` / `Id` fields
**Files:** `MongoDocumentStore.cs`, `BusinessRuleStore.cs`, `ColumnDefinitionStore.cs`, `SettingsStore.cs`, `SchemaRegistryStore.cs`

**Problem:** Both `_id` (MongoDB primary key) and `Id` (C# record property) are stored, duplicating data.

**Target:** Map C# `Id` directly to MongoDB `_id` via `BsonClassMap` or custom serialization, so only `_id` is stored.

### 3. Remove C# `RuleSeedHostedService` seeding (optional)
**File:** `src/gateway/Rules/RuleSeedHostedService.cs`

**Decision:** Make seeding no-op if `seed.js` is the single source of truth, or keep as safety net (skips if data exists — current behavior).

## Already completed (current sprint)

| Item | Status |
|------|--------|
| `[JsonStringEnumConverter]` on `ColumnDataType` | Done (`d3c37da`) |
| `[JsonStringEnumConverter]` on `RuleScope` | Done (`d3c37da`) |
| `seed.js` camelCase field names | Done (`a119552`) |
| Config `Values` → `ValuesJson` | Done (previous commit) |
| Column `DataType` string → number then back to string with converter | Done |

## Migration sprint tasks

1. [x] Refactor `MongoDocumentStore` to `IMongoCollection<T>`
2. [x] Store enums as strings in BSON via `EnumRepresentationConvention(BsonType.String)`; keep `JsonStringEnumConverter` for HTTP responses
3. [x] Map C# `Id` → MongoDB `_id` natively
4. [x] Remove `_id`/`Id` duplication from writes, `seed.js`, and add a legacy cleanup script
5. [x] Evaluate removing C# `RuleSeedHostedService` seeding redundancy — retained as an idempotent safety net
6. [x] Store classes need no changes; the generic `IDocumentStore` methods now use typed collections internally

## Verification checklist for migration sprint

- [x] `dotnet test` passes with typed MongoDB collections (378/378)
- [x] `ng build` production clean
- [x] `ng test` 57/57 pass
- [x] Seed script inserts camelCase documents without duplicate ids; BSON round-trip tests cover every document shape
- [ ] No duplicate `_id`/`Id` fields in MongoDB documents (run `scripts/migrate-remove-duplicate-id.js`)
- [x] Enum values stored as strings in BSON (covered by `MongoBsonConfigurationTests`)
