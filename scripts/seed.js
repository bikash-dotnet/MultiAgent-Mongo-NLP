// MongoDB seed script for MultiAgent-Mongo-NLP
// Compatible with mongosh shell (MongoDB Compass, Studio 3T, mongosh CLI)
// Usage: paste the entire file into the mongosh shell or run:
//   mongosh <database-name> --file scripts/seed.js
//
// The .NET API reads from database configured by MongoDb:Database
// (default: "sample_airbnb"). Run mongosh against that database.
//
// Field naming convention: PascalCase for C# record properties (e.g. ValuesJson,
// SectionKey, UpdatedAt, UpdatedBy). The _id field is the MongoDB document id.
// Enums WITH [JsonStringEnumConverter] serialize as strings:
//   SettingsSource: "Default"/"Database"
//   SchemaDataSource: "Mongo"/"EnterpriseCoreREST"/"Elasticsearch"
//   SchemaFieldType: "String"/"Number"/"Boolean"/"Date"/"Object"/"Array"
//   SettingType: "String"/"Number"/"Double"/"Boolean"/"Select"/"Url"/"Password"/"Port"
//   ColumnDataType: "String"/"Number"/"Currency"/"Boolean"/"Date"
//   RuleScope: "Global"/"Schema"/"Role"

const now = new Date();

// ----------------------------------------------------------------
// Schema definition (mirrors Gateway.Schemas.SchemaSeeder.BuildDefault())
// Source data: src/gateway/Nlp/Assets/Prompts/schema.txt
//              src/gateway/Nlp/Assets/Schema/field_registry.json
// ----------------------------------------------------------------
const schemaFields = [
  { Path: 'name', DataType: 'String', IsSensitive: false, RequiresApproval: false, DataOwnerRoles: [], Description: '' },
  { Path: 'price', DataType: 'Number', IsSensitive: false, RequiresApproval: false, DataOwnerRoles: [], Description: '' },
  { Path: 'room_type', DataType: 'String', IsSensitive: false, RequiresApproval: false, DataOwnerRoles: [], Description: '' },
  { Path: 'accommodates', DataType: 'Number', IsSensitive: false, RequiresApproval: false, DataOwnerRoles: [], Description: '' },
  { Path: 'amenities', DataType: 'Array', IsSensitive: false, RequiresApproval: false, DataOwnerRoles: [], Description: '' },
  { Path: 'beds', DataType: 'Number', IsSensitive: false, RequiresApproval: false, DataOwnerRoles: [], Description: '' },
  { Path: 'bedrooms', DataType: 'Number', IsSensitive: false, RequiresApproval: false, DataOwnerRoles: [], Description: '' },
  { Path: 'address.market', DataType: 'String', IsSensitive: false, RequiresApproval: false, DataOwnerRoles: [], Description: '' },
  { Path: 'property_type', DataType: 'String', IsSensitive: false, RequiresApproval: false, DataOwnerRoles: [], Description: '' },
  { Path: 'review_scores.rating', DataType: 'Number', IsSensitive: false, RequiresApproval: false, DataOwnerRoles: [], Description: '' },
  { Path: 'address.location.coordinates', DataType: 'String', IsSensitive: true, RequiresApproval: true, DataOwnerRoles: ['DataOwner', 'Admin'], Description: '' },
  { Path: 'host.host_verifications', DataType: 'String', IsSensitive: true, RequiresApproval: true, DataOwnerRoles: ['DataOwner', 'Admin'], Description: '' },
  { Path: 'host.host_identity_verified', DataType: 'String', IsSensitive: true, RequiresApproval: false, DataOwnerRoles: ['DataOwner', 'Admin'], Description: '' }
];

const defaultSchema = {
  _id: 'sch_airbnb_listings',
  Id: 'sch_airbnb_listings',
  Name: 'Airbnb Listings',
  DataSource: 'Mongo',
  Database: 'sample_airbnb',
  Collection: 'listingsAndReviews',
  Fields: schemaFields,
  IsActiveSource: true,
  Version: 1,
  Description: 'Default schema seeded from field_registry.json and schema.txt.',
  CreatedAt: now,
  UpdatedAt: now,
  UpdatedBy: 'seed'
};

// ----------------------------------------------------------------
// Configuration defaults (mirrors Gateway.Configuration.SettingsCatalog)
// Seeded as DB overrides in the app_settings collection.
// SettingsDocument record: Id, ValuesJson, Version, Source, UpdatedAt, UpdatedBy
// ----------------------------------------------------------------
const configDefaults = [
  {
    _id: 'Execution',
    SectionKey: 'Execution',
    ValuesJson: JSON.stringify({ DataSource: 'Mongo', TimeoutMs: 5000, Collection: 'listingsAndReviews', Database: 'sample_airbnb' }),
    Version: 1,
    Source: 'Database',
    UpdatedAt: now,
    UpdatedBy: 'seed'
  },
  {
    _id: 'Nlp.Cache',
    SectionKey: 'Nlp.Cache',
    ValuesJson: JSON.stringify({ Threshold: 0.95 }),
    Version: 1,
    Source: 'Database',
    UpdatedAt: now,
    UpdatedBy: 'seed'
  },
  {
    _id: 'Nlp.Defaults',
    SectionKey: 'Nlp.Defaults',
    ValuesJson: JSON.stringify({ Limit: 10, Sort: 'rating_desc', Market: 'All' }),
    Version: 1,
    Source: 'Database',
    UpdatedAt: now,
    UpdatedBy: 'seed'
  },
  {
    _id: 'Nlp.Embeddings',
    SectionKey: 'Nlp.Embeddings',
    ValuesJson: JSON.stringify({ MaxTokens: 512 }),
    Version: 1,
    Source: 'Database',
    UpdatedAt: now,
    UpdatedBy: 'seed'
  },
  {
    _id: 'Governance',
    SectionKey: 'Governance',
    ValuesJson: JSON.stringify({ ApprovalEnabled: true }),
    Version: 1,
    Source: 'Database',
    UpdatedAt: now,
    UpdatedBy: 'seed'
  },
  {
    _id: 'Reports',
    SectionKey: 'Reports',
    ValuesJson: JSON.stringify({ DemoFallbackEnabled: true }),
    Version: 1,
    Source: 'Database',
    UpdatedAt: now,
    UpdatedBy: 'seed'
  },
  {
    _id: 'Reports.Smtp',
    SectionKey: 'Reports.Smtp',
    ValuesJson: JSON.stringify({ Host: 'localhost', Port: 25, User: '', From: '', Password: '' }),
    Version: 1,
    Source: 'Database',
    UpdatedAt: now,
    UpdatedBy: 'seed'
  },
  {
    _id: 'EnterpriseCore',
    SectionKey: 'EnterpriseCore',
    ValuesJson: JSON.stringify({ BaseUrl: '', QueryPath: '/api/query' }),
    Version: 1,
    Source: 'Database',
    UpdatedAt: now,
    UpdatedBy: 'seed'
  },
  {
    _id: 'NvidiaNim',
    SectionKey: 'NvidiaNim',
    ValuesJson: JSON.stringify({ Model: 'nvidia/nemotron-3-nano-omni-30b-a3b-reasoning', BaseUrl: 'https://integrate.api.nvidia.com/v1', MaxAttempts: 3, TimeoutSeconds: 30, ApiKey: '' }),
    Version: 1,
    Source: 'Database',
    UpdatedAt: now,
    UpdatedBy: 'seed'
  },
  {
    _id: 'MongoDb',
    SectionKey: 'MongoDb',
    ValuesJson: JSON.stringify({ ConnectionString: '', Database: 'sample_airbnb' }),
    Version: 1,
    Source: 'Database',
    UpdatedAt: now,
    UpdatedBy: 'seed'
  },
  {
    _id: 'Jwt',
    SectionKey: 'Jwt',
    ValuesJson: JSON.stringify({ Issuer: 'MultiAgentMongoNlp', Audience: 'MultiAgentMongoNlp.Spa', SigningKey: '' }),
    Version: 1,
    Source: 'Database',
    UpdatedAt: now,
    UpdatedBy: 'seed'
  },
  {
    _id: 'Persistence',
    SectionKey: 'Persistence',
    ValuesJson: JSON.stringify({ Mode: 'Mongo', DataDirectory: 'data' }),
    Version: 1,
    Source: 'Database',
    UpdatedAt: now,
    UpdatedBy: 'seed'
  }
];

// ----------------------------------------------------------------
// Business rule defaults (mirrors Gateway.Rules.RuleSeedHostedService)
// BusinessRule record: Id, Name, Description, Scope, ScopeValue, Priority,
//   Enabled, Condition, Action, Version, UpdatedAt, UpdatedBy
// RuleScope uses JsonStringEnumConverter -> strings: "Global"/"Schema"/"Role"
// RuleAction.Parameters is IReadOnlyDictionary<string, string> -> all values are strings
// ----------------------------------------------------------------
const businessRules = [
  {
    _id: 'br_default_limit',
    Id: 'br_default_limit',
    Name: 'Default result limit',
    Description: 'Apply the configured default result limit when the user does not specify one.',
    Scope: 'Global',
    ScopeValue: null,
    Priority: 10,
    Enabled: true,
    Condition: { Field: '*', Operator: 'equals', Values: ['*'], Combine: 'all' },
    Action: { Type: 'setDefault', Parameters: { key: 'limit', value: '10' } },
    Version: 1,
    UpdatedAt: now,
    UpdatedBy: 'seed'
  },
  {
    _id: 'br_deny_destructive',
    Id: 'br_deny_destructive',
    Name: 'Deny destructive operators',
    Description: 'Reject write/admin operators as a defense-in-depth safety rule.',
    Scope: 'Global',
    ScopeValue: null,
    Priority: 20,
    Enabled: true,
    Condition: { Field: '*', Operator: 'equals', Values: ['*'], Combine: 'all' },
    Action: { Type: 'denyOperator', Parameters: { operator: 'delete' } },
    Version: 1,
    UpdatedAt: now,
    UpdatedBy: 'seed'
  },
  {
    _id: 'br_deny_drop',
    Id: 'br_deny_drop',
    Name: 'Deny drop operator',
    Description: 'Reject the drop operator as a defense-in-depth safety rule.',
    Scope: 'Global',
    ScopeValue: null,
    Priority: 21,
    Enabled: true,
    Condition: { Field: '*', Operator: 'equals', Values: ['*'], Combine: 'all' },
    Action: { Type: 'denyOperator', Parameters: { operator: 'drop' } },
    Version: 1,
    UpdatedAt: now,
    UpdatedBy: 'seed'
  }
];

// ----------------------------------------------------------------
// Column definition defaults
// ColumnDefinition record: Id, Field, Label, DataType, Format, Order,
//   VisibleByDefault, Exportable, Sensitive, Width, Version, UpdatedAt, UpdatedBy
// ColumnDataType uses JsonStringEnumConverter -> strings: "String"/"Number"
// ----------------------------------------------------------------
const columnDefinitions = [
  { _id: 'cd_name', Id: 'cd_name', Field: 'name', Label: 'Name', DataType: 'String', Format: null, Order: 0, VisibleByDefault: true, Exportable: true, Sensitive: false, Width: null, Version: 1, UpdatedAt: now, UpdatedBy: 'seed' },
  { _id: 'cd_price', Id: 'cd_price', Field: 'price', Label: 'Price', DataType: 'Number', Format: 'USD', Order: 1, VisibleByDefault: true, Exportable: true, Sensitive: false, Width: null, Version: 1, UpdatedAt: now, UpdatedBy: 'seed' },
  { _id: 'cd_room_type', Id: 'cd_room_type', Field: 'room_type', Label: 'Room Type', DataType: 'String', Format: null, Order: 2, VisibleByDefault: true, Exportable: true, Sensitive: false, Width: null, Version: 1, UpdatedAt: now, UpdatedBy: 'seed' },
  { _id: 'cd_address_market', Id: 'cd_address_market', Field: 'address.market', Label: 'Market', DataType: 'String', Format: null, Order: 3, VisibleByDefault: true, Exportable: true, Sensitive: false, Width: null, Version: 1, UpdatedAt: now, UpdatedBy: 'seed' },
  { _id: 'cd_review_rating', Id: 'cd_review_rating', Field: 'review_scores.rating', Label: 'Rating', DataType: 'Number', Format: null, Order: 4, VisibleByDefault: true, Exportable: true, Sensitive: false, Width: null, Version: 1, UpdatedAt: now, UpdatedBy: 'seed' },
  { _id: 'cd_address_coordinates', Id: 'cd_address_coordinates', Field: 'address.location.coordinates', Label: 'Coordinates', DataType: 'String', Format: null, Order: 10, VisibleByDefault: false, Exportable: false, Sensitive: true, Width: null, Version: 1, UpdatedAt: now, UpdatedBy: 'seed' },
  { _id: 'cd_host_verifications', Id: 'cd_host_verifications', Field: 'host.host_verifications', Label: 'Host Verifications', DataType: 'String', Format: null, Order: 11, VisibleByDefault: false, Exportable: false, Sensitive: true, Width: null, Version: 1, UpdatedAt: now, UpdatedBy: 'seed' }
];

// ----------------------------------------------------------------
// Seed (idempotent: skips documents that already exist)
// ----------------------------------------------------------------
print('Seeding MultiAgent-Mongo-NLP into database: ' + db.getName());

// --- schemas ---
const existingSchemas = db.schemas.countDocuments({});
if (existingSchemas === 0) {
  db.schemas.insertOne(defaultSchema);
  print('[schemas] Inserted default schema: sch_airbnb_listings');
} else {
  print('[schemas] Skipped (' + existingSchemas + ' already exist)');
}

// --- app_settings (config overrides) ---
let settingsInserted = 0;
for (const doc of configDefaults) {
  const exists = db.app_settings.countDocuments({ _id: doc._id });
  if (exists === 0) {
    db.app_settings.insertOne(doc);
    settingsInserted++;
  }
}
print('[app_settings] Inserted ' + settingsInserted + ' config sections');

// --- business_rules ---
let rulesInserted = 0;
for (const doc of businessRules) {
  const exists = db.business_rules.countDocuments({ _id: doc._id });
  if (exists === 0) {
    db.business_rules.insertOne(doc);
    rulesInserted++;
  }
}
print('[business_rules] Inserted ' + rulesInserted + ' rules');

// --- column_definitions ---
let columnsInserted = 0;
for (const doc of columnDefinitions) {
  const exists = db.column_definitions.countDocuments({ _id: doc._id });
  if (exists === 0) {
    db.column_definitions.insertOne(doc);
    columnsInserted++;
  }
}
print('[column_definitions] Inserted ' + columnsInserted + ' columns');

print('\nSeed complete.');
