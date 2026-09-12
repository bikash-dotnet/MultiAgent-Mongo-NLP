#!/usr/bin/env node
'use strict';

const { Server } = require('@modelcontextprotocol/sdk/server/index.js');
const { StdioServerTransport } = require('@modelcontextprotocol/sdk/server/stdio.js');
const {
  CallToolRequestSchema,
  ListToolsRequestSchema,
} = require('@modelcontextprotocol/sdk/types.js');
const { MongoClient } = require('mongodb');

const {
  sampleCollection, fetchIndexes,
  inferFlattenedSchema, toCompactSchema, retrieveRelevantFields,
  buildPrompt, callGemini, parseEJSON, validatePipeline, CONFIG,
} = require('./mongo-ai-query.js');

// ── Schema cache
const schemaCache = new Map();
const SCHEMA_TTL_MS = 5 * 60 * 1000;

async function getSchemaAndIndexes(client, dbName, collName) {
  const key = `${dbName}.${collName}`;
  const c = schemaCache.get(key);
  if (c && Date.now() - c.ts < SCHEMA_TTL_MS) return c;

  const [docs, indexes] = await Promise.all([
    sampleCollection(client, dbName, collName, CONFIG.sampleSize),
    fetchIndexes(client, dbName, collName),
  ]);
  const schema = inferFlattenedSchema(docs);
  schema.collection = key;
  const entry = { schema, indexes, ts: Date.now() };
  schemaCache.set(key, entry);
  return entry;
}

// ── Server
const server = new Server(
  { name: 'mongo-ai-query', version: '3.0.0' },
  { capabilities: { tools: {} } }
);

server.setRequestHandler(ListToolsRequestSchema, async () => ({
  tools: [
    {
      name: 'get_schema',
      description: 'Return the flattened, BSON-typed schema for a MongoDB collection.',
      inputSchema: {
        type: 'object',
        properties: {
          db:         { type: 'string' },
          collection: { type: 'string' },
        },
        required: ['db', 'collection'],
      },
    },
    {
      name: 'generate_pipeline',
      description: 'Generate a MongoDB aggregation pipeline from natural language, validated against the schema.',
      inputSchema: {
        type: 'object',
        properties: {
          db:         { type: 'string' },
          collection: { type: 'string' },
          question:   { type: 'string' },
        },
        required: ['db', 'collection', 'question'],
      },
    },
    {
      name: 'run_pipeline',
      description: 'Validate and execute an EJSON-encoded MongoDB aggregation pipeline.',
      inputSchema: {
        type: 'object',
        properties: {
          db:         { type: 'string' },
          collection: { type: 'string' },
          pipeline:   { type: 'array' },
        },
        required: ['db', 'collection', 'pipeline'],
      },
    },
    {
      name: 'ask',
      description: 'End-to-end: NL → schema → Gemini → validate → execute. Returns pipeline + results.',
      inputSchema: {
        type: 'object',
        properties: {
          db:         { type: 'string' },
          collection: { type: 'string' },
          question:   { type: 'string' },
        },
        required: ['db', 'collection', 'question'],
      },
    },
  ],
}));

server.setRequestHandler(CallToolRequestSchema, async (req) => {
  const { name, arguments: args } = req.params;
  const client = new MongoClient(CONFIG.mongoUri);
  await client.connect();

  try {
    if (name === 'get_schema') {
      const { schema } = await getSchemaAndIndexes(client, args.db, args.collection);
      return { content: [{ type: 'text', text: JSON.stringify(schema, null, 2) }] };
    }

    if (name === 'generate_pipeline' || name === 'ask') {
      const { schema, indexes } = await getSchemaAndIndexes(client, args.db, args.collection);
      const relevant = retrieveRelevantFields(schema, args.question, CONFIG.maxFields);
      const compact  = toCompactSchema(schema, relevant);
      const prompt   = buildPrompt(compact, args.question, { indexes });

      const raw = CONFIG.useCache
        ? await callGemini(args.question, compact, schema, indexes)
        : await callGemini(prompt, null, null, null);

      const v = validatePipeline(raw, schema);

      if (name === 'generate_pipeline') {
        return { content: [{ type: 'text', text: JSON.stringify({
          ok: v.ok, pipeline: v.pipeline, errors: v.errors, warnings: v.warnings,
        }, null, 2) }] };
      }

      if (!v.ok) {
        return { isError: true, content: [{ type: 'text',
          text: `Invalid pipeline:\n${v.errors.join('\n')}\nRaw:\n${raw}` }] };
      }
      const results = await client.db(args.db).collection(args.collection)
        .aggregate(parseEJSON(v.pipeline))
        .limit(CONFIG.resultLimit)
        .toArray();

      return { content: [{ type: 'text', text: JSON.stringify({
        pipeline: v.pipeline, warnings: v.warnings,
        resultCount: results.length, results,
      }, null, 2) }] };
    }

    if (name === 'run_pipeline') {
      const { schema } = await getSchemaAndIndexes(client, args.db, args.collection);
      const v = validatePipeline(args.pipeline, schema);
      if (!v.ok) {
        return { isError: true, content: [{ type: 'text',
          text: `Validation failed:\n${v.errors.join('\n')}` }] };
      }
      const results = await client.db(args.db).collection(args.collection)
        .aggregate(parseEJSON(v.pipeline))
        .limit(CONFIG.resultLimit)
        .toArray();
      return { content: [{ type: 'text', text: JSON.stringify(results, null, 2) }] };
    }

    return { isError: true, content: [{ type: 'text', text: `Unknown tool: ${name}` }] };
  } catch (e) {
    return { isError: true, content: [{ type: 'text', text: `Error: ${e.message}` }] };
  } finally {
    await client.close();
  }
});

(async () => {
  const transport = new StdioServerTransport();
  await server.connect(transport);
  console.error('mongo-ai-query MCP server v3 running on stdio');
})();