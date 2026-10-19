// One-time cleanup for the native MongoDB driver migration.
// Removes legacy duplicate id fields now that _id maps directly to Id
// (or SessionId for agent_states). Run against the gateway database:
//   mongosh <database-name> --file scripts/migrate-remove-duplicate-id.js

const collections = [
  'business_rules',
  'column_definitions',
  'schemas',
  'app_settings',
  'mcp_servers',
  'conversations',
  'access_requests',
  'audit_logs'
];

for (const name of collections) {
  const result = db.getCollection(name).updateMany(
    { $or: [{ id: { $exists: true } }, { Id: { $exists: true } }] },
    { $unset: { id: '', Id: '' } }
  );
  print(`${name}: removed duplicate id from ${result.modifiedCount} document(s)`);
}

const agentStates = db.getCollection('agent_states').updateMany(
  { $or: [{ sessionId: { $exists: true } }, { SessionId: { $exists: true } }] },
  { $unset: { sessionId: '', SessionId: '' } }
);
print(`agent_states: removed duplicate sessionId from ${agentStates.modifiedCount} document(s)`);
