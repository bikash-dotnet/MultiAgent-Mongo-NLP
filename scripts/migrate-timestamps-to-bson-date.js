// One-time migration from ISO-8601 string timestamps to BSON Date values.
// Run while the gateway is stopped, before starting the BSON Date build (the new
// serializer does not read string timestamps):
//   mongosh <database-name> --file scripts/migrate-timestamps-to-bson-date.js
// Idempotent: only string timestamps with an ISO-8601 date prefix are converted.

const targets = [
  ['schemas', ['createdAt', 'updatedAt']],
  ['app_settings', ['updatedAt']],
  ['business_rules', ['updatedAt']],
  ['column_definitions', ['updatedAt']],
  ['mcp_servers', ['updatedAt']],
  ['conversations', ['createdAt', 'updatedAt']],
  ['access_requests', ['createdAt', 'resolution.resolvedAt']],
  ['audit_logs', ['auditTimestamp']],
  ['agent_states', ['updatedAt']],
  ['schema_locks', ['expiresAt']]
];

const isoPrefix = /^\d{4}-\d{2}-\d{2}T/;

function readPath(document, path) {
  return path.split('.').reduce((value, key) => (value == null ? value : value[key]), document);
}

for (const [collectionName, fields] of targets) {
  const collection = db.getCollection(collectionName);
  for (const field of fields) {
    let converted = 0;
    let skipped = 0;

    collection
      .find({ [field]: { $type: 'string' } }, { [field]: 1 })
      .forEach((document) => {
        const raw = readPath(document, field);
        if (typeof raw !== 'string' || !isoPrefix.test(raw)) {
          skipped++;
          return;
        }

        const date = new Date(raw);
        if (Number.isNaN(date.getTime())) {
          skipped++;
          return;
        }

        collection.updateOne({ _id: document._id }, { $set: { [field]: date } });
        converted++;
      });

    if (converted > 0 || skipped > 0) {
      print(`${collectionName}.${field}: converted ${converted}, skipped ${skipped}`);
    }
  }
}
