# Migration Sprint Plan — MongoDB Native Driver + Convention Alignment

## Deferred from current sprint (Option A applied now)

### 1. Native MongoDB Driver Migration (`MongoDocumentStore`)
**File:** `src/gateway/Persistence/MongoDocumentStore.cs`

**Current:** System.Text.Json round-trip (C# → JSON → BsonDocument → MongoDB)
**Target:** `IMongoCollection<T>` with `BsonClassMap` direct serialization

**Changes:**
- Replace `IMongoCollection<BsonDocument>` with `IMongoCollection<T>` per document type
- Remove `BsonDocument.Parse(JsonSerializer.Serialize(...))` round-trip
- Use driver's native `ReplaceOneAsync`/`FindAsync` with typed filters
- Enable MongoDB BSON attributes (`[BsonRepresentation]`, `[BsonElement]`, `[BsonIgnore]`)

**Benefits:**
- Eliminates JSON serialization overhead on every read/write
- `[BsonRepresentation(BsonType.String)]` enums work natively (no JsonConverter needed)
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

## Migration sprint tasks (not started)

1. Refactor `MongoDocumentStore` to `IMongoCollection<T>`
2. Add `[BsonRepresentation(BsonType.String)]` to enums (replacing JsonConverter)
3. Map C# `Id` → MongoDB `_id` natively
4. Remove `_id`/`Id` duplication from all stored documents
5. Evaluate removing C# `RuleSeedHostedService` seeding redundancy
6. Update all store classes (`BusinessRuleStore`, `ColumnDefinitionStore`, `SettingsStore`, `SchemaRegistryStore`) for typed collections

## Verification checklist for migration sprint

- [ ] `dotnet test` passes with typed MongoDB collections
- [ ] `ng build` production clean
- [ ] `ng test` 48/48 pass
- [ ] Seed script inserts camelCase documents that deserialize correctly
- [ ] No duplicate `_id`/`Id` fields in MongoDB documents
- [ ] Enum values stored as strings in MongoDB (verifiable via Compass/Studio 3T)
