// MongoDB seed script for MultiAgent-Mongo-NLP
// Compatible with mongosh shell (MongoDB Compass, Studio 3T, mongosh CLI)
// Usage: paste the entire file into the mongosh shell or run:
//   mongosh --file scripts/seed.js
//
// Optional env vars (set in your shell before running mongosh):
//   MONGODB_DB=multiagent   (default: multiagent)
//   MONGODB_URI             (passed to mongosh connection string)

// ----------------------------------------------------------------
// Helpers
// ----------------------------------------------------------------
const now = new Date();
const dbName = (typeof __dbName !== 'undefined' && __dbName) ? __dbName : db.getName();

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
  Id: 'sch_airbnb_listings',
  Name: 'Airbnb Listings',
  DataSource: 'Mongo',
  Database: 'sample_airbnb',
  Collection: 'listingsAndReviews',
  Fields: schemaFields,
  IsActiveSource: true,
  Version: 1,
  Description: 'Default schema seeded from field_registry.json and schema.txt.',
  UpdatedAt: now
};

// ----------------------------------------------------------------
// Configuration defaults (mirrors Gateway.Configuration.SettingsCatalog)
// Seeded as DB overrides in the app_settings collection.
// ----------------------------------------------------------------
const configDefaults = [
  {
    _id: 'Execution',
    SectionKey: 'Execution',
    Values: JSON.stringify({ DataSource: 'Mongo', TimeoutMs: 5000, Collection: 'listingsAndReviews', Database: 'sample_airbnb' }),
    Version: 1,
    Source: 'Database',
    UpdatedAt: now,
    UpdatedBy: 'seed'
  },
  {
    _id: 'Nlp.Cache',
    SectionKey: 'Nlp.Cache',
    Values: JSON.stringify({ Threshold: 0.95 }),
    Version: 1,
    Source: 'Database',
    UpdatedAt: now,
    UpdatedBy: 'seed'
  },
  {
    _id: 'Nlp.Defaults',
    SectionKey: 'Nlp.Defaults',
    Values: JSON.stringify({ Limit: 10, Sort: 'rating_desc', Market: 'All' }),
    Version: 1,
    Source: 'Database',
    UpdatedAt: now,
    UpdatedBy: 'seed'
  },
  {
    _id: 'Nlp.Embeddings',
    SectionKey: 'Nlp.Embeddings',
    Values: JSON.stringify({ MaxTokens: 512 }),
    Version: 1,
    Source: 'Database',
    UpdatedAt: now,
    UpdatedBy: 'seed'
  },
  {
    _id: 'Governance',
    SectionKey: 'Governance',
    Values: JSON.stringify({ ApprovalEnabled: true }),
    Version: 1,
    Source: 'Database',
    UpdatedAt: now,
    UpdatedBy: 'seed'
  },
  {
    _id: 'Reports',
    SectionKey: 'Reports',
    Values: JSON.stringify({ DemoFallbackEnabled: true }),
    Version: 1,
    Source: 'Database',
    UpdatedAt: now,
    UpdatedBy: 'seed'
  },
  {
    _id: 'Reports.Smtp',
    SectionKey: 'Reports.Smtp',
    Values: JSON.stringify({ Host: 'localhost', Port: 25, User: '', From: '', Password: '' }),
    Version: 1,
    Source: 'Database',
    UpdatedAt: now,
    UpdatedBy: 'seed'
  },
  {
    _id: 'EnterpriseCore',
    SectionKey: 'EnterpriseCore',
    Values: JSON.stringify({ BaseUrl: '', QueryPath: '/api/query' }),
    Version: 1,
    Source: 'Database',
    UpdatedAt: now,
    UpdatedBy: 'seed'
  },
  {
    _id: 'NvidiaNim',
    SectionKey: 'NvidiaNim',
    Values: JSON.stringify({ Model: 'nvidia/nemotron-3-nano-omni-30b-a3b-reasoning', BaseUrl: 'https://integrate.api.nvidia.com/v1', MaxAttempts: 3, TimeoutSeconds: 30, ApiKey: '' }),
    Version: 1,
    Source: 'Database',
    UpdatedAt: now,
    UpdatedBy: 'seed'
  },
  {
    _id: 'MongoDb',
    SectionKey: 'MongoDb',
    Values: JSON.stringify({ ConnectionString: '', Database: 'sample_airbnb' }),
    Version: 1,
    Source: 'Database',
    UpdatedAt: now,
    UpdatedBy: 'seed'
  },
  {
    _id: 'Jwt',
    SectionKey: 'Jwt',
    Values: JSON.stringify({ Issuer: 'MultiAgentMongoNlp', Audience: 'MultiAgentMongoNlp.Spa', SigningKey: '' }),
    Version: 1,
    Source: 'Database',
    UpdatedAt: now,
    UpdatedBy: 'seed'
  },
  {
    _id: 'Persistence',
    SectionKey: 'Persistence',
    Values: JSON.stringify({ Mode: 'Mongo', DataDirectory: 'data' }),
    Version: 1,
    Source: 'Database',
    UpdatedAt: now,
    UpdatedBy: 'seed'
  }
];

// ----------------------------------------------------------------
// Business rule defaults (Sprint 12 seeded rules)
// ----------------------------------------------------------------
const businessRules = [
  {
    _id: 'br_default_limit',
    Id: 'br_default_limit',
    Name: 'Default result limit',
    Scope: 'Global',
    ScopeValue: null,
    Priority: 100,
    Enabled: true,
    Condition: { Field: null, Operator: null, Values: [], Combine: 'AND' },
    Actions: [ { Type: 'setDefault', Parameters: { key: 'Nlp.Defaults.Limit', value: 10 } } ],
    Version: 1,
    UpdatedAt: now,
    UpdatedBy: 'seed'
  },
  {
    _id: 'br_mask_sensitive',
    Id: 'br_mask_sensitive',
    Name: 'Mask sensitive fields',
    Scope: 'Role',
    ScopeValue: 'Analyst',
    Priority: 50,
    Enabled: true,
    Condition: { Field: null, Operator: null, Values: [], Combine: 'AND' },
    Actions: [
      { Type: 'maskColumn', Parameters: { field: 'address.location.coordinates' } },
      { Type: 'maskColumn', Parameters: { field: 'host.host_verifications' } },
      { Type: 'maskColumn', Parameters: { field: 'host.host_identity_verified' } }
    ],
    Version: 1,
    UpdatedAt: now,
    UpdatedBy: 'seed'
  },
  {
    _id: 'br_exempt_data_owner',
    Id: 'br_exempt_data_owner',
    Name: 'Exempt Data Owner from masking',
    Scope: 'Role',
    ScopeValue: 'Data Owner / Admin',
    Priority: 10,
    Enabled: true,
    Condition: { Field: null, Operator: null, Values: [], Combine: 'AND' },
    Actions: [ { Type: 'exemptRole', Parameters: { role: 'Data Owner / Admin' } } ],
    Version: 1,
    UpdatedAt: now,
    UpdatedBy: 'seed'
  }
];

// ----------------------------------------------------------------
// Column definition defaults
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
print('Seeding MultiAgent-Mongo-NLP into database: ' + dbName);

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
