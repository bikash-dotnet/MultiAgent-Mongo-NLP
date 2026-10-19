// MongoDB seed script for MultiAgent-Mongo-NLP
// Compatible with mongosh shell (MongoDB Compass, Studio 3T, mongosh CLI)
// Usage: paste the entire file into the mongosh shell or run:
//   mongosh <database-name> --file scripts/seed.js
//
// The .NET API reads from database configured by MongoDb:Database
// (default: "sample_airbnb"). Run mongosh against that database.
//
// Field naming convention: camelCase to match C# System.Text.Json serialization
// (JsonSerializerDefaults.Web). The _id field is the MongoDB document id.
// Id property is duplicated alongside _id for C# model compatibility
// (MongoDocumentStore copies Id -> _id on write, strips _id on read).
//
// Enums WITH [JsonStringEnumConverter] serialize as strings:
//   SettingsSource: "Default"/"Database"
//   SchemaDataSource: "Mongo"/"EnterpriseCoreREST"/"Elasticsearch"
//   SchemaFieldType: "String"/"Number"/"Boolean"/"Date"/"Object"/"Array"
//   SettingType: "String"/"Number"/"Double"/"Boolean"/"Select"/"Url"/"Password"/"Port"
//   ColumnDataType: "String"/"Number"/"Currency"/"Boolean"/"Date"
//   RuleScope: "Global"/"Schema"/"Role"

const now = new Date().toISOString();

// ----------------------------------------------------------------
// Schema definition (mirrors Gateway.Schemas.SchemaSeeder.BuildDefault())
// Source data: src/gateway/Nlp/Assets/Prompts/schema.txt
//              src/gateway/Nlp/Assets/Schema/field_registry.json
// ----------------------------------------------------------------
const schemaFields = [
  { path: 'name', dataType: 'String', isSensitive: false, requiresApproval: false, dataOwnerRoles: [], description: '' },
  { path: 'price', dataType: 'Number', isSensitive: false, requiresApproval: false, dataOwnerRoles: [], description: '' },
  { path: 'room_type', dataType: 'String', isSensitive: false, requiresApproval: false, dataOwnerRoles: [], description: '' },
  { path: 'accommodates', dataType: 'Number', isSensitive: false, requiresApproval: false, dataOwnerRoles: [], description: '' },
  { path: 'amenities', dataType: 'Array', isSensitive: false, requiresApproval: false, dataOwnerRoles: [], description: '' },
  { path: 'beds', dataType: 'Number', isSensitive: false, requiresApproval: false, dataOwnerRoles: [], description: '' },
  { path: 'bedrooms', dataType: 'Number', isSensitive: false, requiresApproval: false, dataOwnerRoles: [], description: '' },
  { path: 'address.market', dataType: 'String', isSensitive: false, requiresApproval: false, dataOwnerRoles: [], description: '' },
  { path: 'property_type', dataType: 'String', isSensitive: false, requiresApproval: false, dataOwnerRoles: [], description: '' },
  { path: 'review_scores.rating', dataType: 'Number', isSensitive: false, requiresApproval: false, dataOwnerRoles: [], description: '' },
  { path: 'address.location.coordinates', dataType: 'String', isSensitive: true, requiresApproval: true, dataOwnerRoles: ['DataOwner', 'Admin'], description: '' },
  { path: 'host.host_verifications', dataType: 'String', isSensitive: true, requiresApproval: true, dataOwnerRoles: ['DataOwner', 'Admin'], description: '' },
  { path: 'host.host_identity_verified', dataType: 'String', isSensitive: true, requiresApproval: false, dataOwnerRoles: ['DataOwner', 'Admin'], description: '' }
];

const defaultSchema = {
  _id: 'sch_airbnb_listings',
  id: 'sch_airbnb_listings',
  name: 'Airbnb Listings',
  dataSource: 'Mongo',
  database: 'sample_airbnb',
  collection: 'listingsAndReviews',
  fields: schemaFields,
  isActiveSource: true,
  version: 1,
  description: 'Default schema seeded from field_registry.json and schema.txt.',
  createdAt: now,
  updatedAt: now,
  updatedBy: 'seed'
};

// ----------------------------------------------------------------
// Configuration defaults (mirrors Gateway.Configuration.SettingsCatalog)
// Seeded as DB overrides in the app_settings collection.
// SettingsDocument record: id, valuesJson, version, source, updatedAt, updatedBy
// ----------------------------------------------------------------
const configDefaults = [
  {
    _id: 'Execution',
    id: 'Execution',
    sectionKey: 'Execution',
    valuesJson: JSON.stringify({ dataSource: 'Mongo', timeoutMs: 5000, collection: 'listingsAndReviews', database: 'sample_airbnb' }),
    version: 1,
    source: 'Database',
    updatedAt: now,
    updatedBy: 'seed'
  },
  {
    _id: 'Nlp.Cache',
    id: 'Nlp.Cache',
    sectionKey: 'Nlp.Cache',
    valuesJson: JSON.stringify({ threshold: 0.95 }),
    version: 1,
    source: 'Database',
    updatedAt: now,
    updatedBy: 'seed'
  },
  {
    _id: 'Nlp.Defaults',
    id: 'Nlp.Defaults',
    sectionKey: 'Nlp.Defaults',
    valuesJson: JSON.stringify({ limit: 10, sort: 'rating_desc', market: 'All' }),
    version: 1,
    source: 'Database',
    updatedAt: now,
    updatedBy: 'seed'
  },
  {
    _id: 'Nlp.Embeddings',
    id: 'Nlp.Embeddings',
    sectionKey: 'Nlp.Embeddings',
    valuesJson: JSON.stringify({ maxTokens: 512 }),
    version: 1,
    source: 'Database',
    updatedAt: now,
    updatedBy: 'seed'
  },
  {
    _id: 'Governance',
    id: 'Governance',
    sectionKey: 'Governance',
    valuesJson: JSON.stringify({ approvalEnabled: true }),
    version: 1,
    source: 'Database',
    updatedAt: now,
    updatedBy: 'seed'
  },
  {
    _id: 'Reports',
    id: 'Reports',
    sectionKey: 'Reports',
    valuesJson: JSON.stringify({ demoFallbackEnabled: true }),
    version: 1,
    source: 'Database',
    updatedAt: now,
    updatedBy: 'seed'
  },
  {
    _id: 'Reports.Smtp',
    id: 'Reports.Smtp',
    sectionKey: 'Reports.Smtp',
    valuesJson: JSON.stringify({ host: 'localhost', port: 25, user: '', from: '', password: '' }),
    version: 1,
    source: 'Database',
    updatedAt: now,
    updatedBy: 'seed'
  },
  {
    _id: 'EnterpriseCore',
    id: 'EnterpriseCore',
    sectionKey: 'EnterpriseCore',
    valuesJson: JSON.stringify({ baseUrl: '', queryPath: '/api/query' }),
    version: 1,
    source: 'Database',
    updatedAt: now,
    updatedBy: 'seed'
  },
  {
    _id: 'NvidiaNim',
    id: 'NvidiaNim',
    sectionKey: 'NvidiaNim',
    valuesJson: JSON.stringify({ model: 'nvidia/nemotron-3-nano-omni-30b-a3b-reasoning', baseUrl: 'https://integrate.api.nvidia.com/v1', maxAttempts: 3, timeoutSeconds: 30, apiKey: '' }),
    version: 1,
    source: 'Database',
    updatedAt: now,
    updatedBy: 'seed'
  },
  {
    _id: 'MongoDb',
    id: 'MongoDb',
    sectionKey: 'MongoDb',
    valuesJson: JSON.stringify({ connectionString: '', database: 'sample_airbnb' }),
    version: 1,
    source: 'Database',
    updatedAt: now,
    updatedBy: 'seed'
  },
  {
    _id: 'Jwt',
    id: 'Jwt',
    sectionKey: 'Jwt',
    valuesJson: JSON.stringify({ issuer: 'MultiAgentMongoNlp', audience: 'MultiAgentMongoNlp.Spa', signingKey: '' }),
    version: 1,
    source: 'Database',
    updatedAt: now,
    updatedBy: 'seed'
  },
  {
    _id: 'Persistence',
    id: 'Persistence',
    sectionKey: 'Persistence',
    valuesJson: JSON.stringify({ mode: 'Mongo', dataDirectory: 'data' }),
    version: 1,
    source: 'Database',
    updatedAt: now,
    updatedBy: 'seed'
  }
];

// ----------------------------------------------------------------
// Business rule defaults (mirrors Gateway.Rules.RuleSeedHostedService)
// BusinessRule record: id, name, description, scope, scopeValue, priority,
//   enabled, condition, action, version, updatedAt, updatedBy
// RuleScope uses JsonStringEnumConverter -> strings: "Global"/"Schema"/"Role"
// RuleAction.Parameters is IReadOnlyDictionary<string, string> -> all values are strings
// ----------------------------------------------------------------
const businessRules = [
  {
    _id: 'br_default_limit',
    id: 'br_default_limit',
    name: 'Default result limit',
    description: 'Apply the configured default result limit when the user does not specify one.',
    scope: 'Global',
    scopeValue: null,
    priority: 10,
    enabled: true,
    condition: { field: '*', operator: 'equals', values: ['*'], combine: 'all' },
    action: { type: 'setDefault', parameters: { key: 'limit', value: '10' } },
    version: 1,
    updatedAt: now,
    updatedBy: 'seed'
  },
  {
    _id: 'br_deny_destructive',
    id: 'br_deny_destructive',
    name: 'Deny destructive operators',
    description: 'Reject write/admin operators as a defense-in-depth safety rule.',
    scope: 'Global',
    scopeValue: null,
    priority: 20,
    enabled: true,
    condition: { field: '*', operator: 'equals', values: ['*'], combine: 'all' },
    action: { type: 'denyOperator', parameters: { operator: 'delete' } },
    version: 1,
    updatedAt: now,
    updatedBy: 'seed'
  },
  {
    _id: 'br_deny_drop',
    id: 'br_deny_drop',
    name: 'Deny drop operator',
    description: 'Reject the drop operator as a defense-in-depth safety rule.',
    scope: 'Global',
    scopeValue: null,
    priority: 21,
    enabled: true,
    condition: { field: '*', operator: 'equals', values: ['*'], combine: 'all' },
    action: { type: 'denyOperator', parameters: { operator: 'drop' } },
    version: 1,
    updatedAt: now,
    updatedBy: 'seed'
  }
];

// ----------------------------------------------------------------
// Column definition defaults
// ColumnDefinition record: id, field, label, dataType, format, order,
//   visibleByDefault, exportable, sensitive, width, version, updatedAt, updatedBy
// ColumnDataType uses JsonStringEnumConverter -> strings: "String"/"Number"
// ----------------------------------------------------------------
const columnDefinitions = [
  { _id: 'cd_name', id: 'cd_name', field: 'name', label: 'Name', dataType: 'String', format: null, order: 0, visibleByDefault: true, exportable: true, sensitive: false, width: null, version: 1, updatedAt: now, updatedBy: 'seed' },
  { _id: 'cd_price', id: 'cd_price', field: 'price', label: 'Price', dataType: 'Number', format: 'USD', order: 1, visibleByDefault: true, exportable: true, sensitive: false, width: null, version: 1, updatedAt: now, updatedBy: 'seed' },
  { _id: 'cd_room_type', id: 'cd_room_type', field: 'room_type', label: 'Room Type', dataType: 'String', format: null, order: 2, visibleByDefault: true, exportable: true, sensitive: false, width: null, version: 1, updatedAt: now, updatedBy: 'seed' },
  { _id: 'cd_address_market', id: 'cd_address_market', field: 'address.market', label: 'Market', dataType: 'String', format: null, order: 3, visibleByDefault: true, exportable: true, sensitive: false, width: null, version: 1, updatedAt: now, updatedBy: 'seed' },
  { _id: 'cd_review_rating', id: 'cd_review_rating', field: 'review_scores.rating', label: 'Rating', dataType: 'Number', format: null, order: 4, visibleByDefault: true, exportable: true, sensitive: false, width: null, version: 1, updatedAt: now, updatedBy: 'seed' },
  { _id: 'cd_address_coordinates', id: 'cd_address_coordinates', field: 'address.location.coordinates', label: 'Coordinates', dataType: 'String', format: null, order: 10, visibleByDefault: false, exportable: false, sensitive: true, width: null, version: 1, updatedAt: now, updatedBy: 'seed' },
  { _id: 'cd_host_verifications', id: 'cd_host_verifications', field: 'host.host_verifications', label: 'Host Verifications', dataType: 'String', format: null, order: 11, visibleByDefault: false, exportable: false, sensitive: true, width: null, version: 1, updatedAt: now, updatedBy: 'seed' }
];

// ----------------------------------------------------------------
// Seed (upsert: replaces existing documents, ensuring camelCase field names)
// Old PascalCase documents are overwritten on re-run.
// ----------------------------------------------------------------
print('Seeding MultiAgent-Mongo-NLP into database: ' + db.getName());

// --- schemas ---
db.schemas.replaceOne({ _id: defaultSchema._id }, defaultSchema, { upsert: true });
print('[schemas] Upserted default schema: sch_airbnb_listings');

// --- app_settings (config overrides) ---
let settingsUpserted = 0;
for (const doc of configDefaults) {
  db.app_settings.replaceOne({ _id: doc._id }, doc, { upsert: true });
  settingsUpserted++;
}
print('[app_settings] Upserted ' + settingsUpserted + ' config sections');

// --- business_rules ---
let rulesUpserted = 0;
for (const doc of businessRules) {
  db.business_rules.replaceOne({ _id: doc._id }, doc, { upsert: true });
  rulesUpserted++;
}
print('[business_rules] Upserted ' + rulesUpserted + ' rules');

// --- column_definitions ---
let columnsUpserted = 0;
for (const doc of columnDefinitions) {
  db.column_definitions.replaceOne({ _id: doc._id }, doc, { upsert: true });
  columnsUpserted++;
}
print('[column_definitions] Upserted ' + columnsUpserted + ' columns');

print('\nSeed complete.');
