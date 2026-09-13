#!/usr/bin/env node
/**
 * mongo-ai-query.js  (v3)
 *
 * End-to-end: MongoDB → sampled schema → compact prompt → Gemini → validated
 * MongoDB aggregation pipeline → executed results.
 *
 * v3 highlights:
 *   - MongoDB official best-practice rules baked into the prompt
 *   - Index-aware prompt (fetches real indexes)
 *   - Strict validator mirroring the prompt rules
 *   - Compact schema + field retrieval + Gemini context caching
 *   - MCP-ready exports
 *
 * Usage:
 *   node mongo-ai-query.js --db shop --collection users \
 *     --ask "top 5 users in Pune by total order amount"
 *
 * Env:
 *   GEMINI_API_KEY, GEMINI_MODEL, MONGO_URI, SAMPLE_SIZE,
 *   MAX_FIELDS, USE_CACHE
 */

'use strict';

const { MongoClient, ObjectId, Decimal128, Long, Binary, Timestamp } = require('mongodb');
const { GoogleGenerativeAI } = require('@google/generative-ai');
// config.js
require('dotenv').config();
// ─────────────────────────────────────────────────────────────────────────────
// CONFIG
// ─────────────────────────────────────────────────────────────────────────────
const CONFIG = {
  mongoUri:    process.env.MONGO_URI     || 'mongodb://localhost:27017',
  dbName:      process.env.MONGO_DB      || 'shop',
  collection:  process.env.MONGO_COLL    || 'users',
  geminiKey:   process.env.GEMINI_API_KEY || process.env.API_KEY,
  geminiModel: process.env.GEMINI_MODEL || process.env.MODEL || 'gemini-1.5-pro',
  sampleSize:  parseInt(process.env.SAMPLE_SIZE || '200', 10),
  maxFields:   parseInt(process.env.MAX_FIELDS || '40', 10),
  useCache:    process.env.USE_CACHE === '1',
  resultLimit: parseInt(process.env.RESULT_LIMIT || '100', 10),
};

const ALLOWED_STAGES = new Set([
  '$match','$project','$group','$sort','$limit','$skip',
  '$unwind','$lookup','$addFields','$count','$facet',
  '$bucket','$bucketAuto','$sample','$replaceRoot',
  '$replaceWith','$set','$unset','$redact',
]);

const BANNED_STAGES = new Set([
  '$where','$function','$accumulator','$out','$merge',
  '$currentOp','$listSessions','$planCacheStats',
]);

const TYPE_ALIAS = {
  objectId:'oid', string:'str', date:'date', decimal:'dec',
  long:'lng', int:'int', double:'dbl', boolean:'bool',
  binary:'bin', timestamp:'ts', regex:'rx', null:'null',
  object:'obj',
};
const ALIAS_TO_TYPE = Object.fromEntries(
  Object.entries(TYPE_ALIAS).map(([k, v]) => [v, k])
);

// ─────────────────────────────────────────────────────────────────────────────
// 1. SAMPLE
// ─────────────────────────────────────────────────────────────────────────────
async function sampleCollection(client, dbName, collName, size) {
  const coll = client.db(dbName).collection(collName);
  const docs = await coll.aggregate([{ $sample: { size } }]).toArray();
  if (!docs.length) throw new Error(`No documents in ${dbName}.${collName}`);
  return docs;
}

// ─────────────────────────────────────────────────────────────────────────────
// 2. INDEXES
// ─────────────────────────────────────────────────────────────────────────────
async function fetchIndexes(client, dbName, collName) {
  try {
    const idx = await client.db(dbName).collection(collName).indexes();
    return idx
      .filter(i => i.name !== '_id_')
      .map(i => ({ keys: i.key, unique: !!i.unique }));
  } catch {
    return [];
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// 3. BSON TYPE DETECTION
// ─────────────────────────────────────────────────────────────────────────────
function bsonTypeOf(v) {
  if (v === null)              return 'null';
  if (v === undefined)         return 'undefined';
  if (v instanceof ObjectId)   return 'objectId';
  if (v instanceof Date)       return 'date';
  if (v instanceof Decimal128) return 'decimal';
  if (v instanceof Long)       return 'long';
  if (v instanceof Binary)     return 'binary';
  if (v instanceof Timestamp)  return 'timestamp';
  if (v instanceof RegExp)     return 'regex';
  if (Array.isArray(v))        return 'array';
  if (typeof v === 'string')   return 'string';
  if (typeof v === 'boolean')  return 'boolean';
  if (typeof v === 'number')   return Number.isInteger(v) ? 'int' : 'double';
  if (typeof v === 'bigint')   return 'long';
  if (typeof v === 'object')   return 'object';
  return 'unknown';
}

// ─────────────────────────────────────────────────────────────────────────────
// 4. FULL FLATTENED SCHEMA INFERENCE
// ─────────────────────────────────────────────────────────────────────────────
function inferFlattenedSchema(docs) {
  const fields = {};
  const arrayMeta = {};
  const pathCounts = {};
  const total = docs.length;

  function newField() {
    return { types: new Set(), array: false, enums: new Set(), sampleCount: 0 };
  }

  function walk(value, path, inArray) {
    const t = bsonTypeOf(value);

    if (t === 'array') {
      fields[path] = fields[path] || newField();
      fields[path].array = true;
      fields[path].types.add('array');

      const elTypes = new Set(value.map(bsonTypeOf));
      const isObjArr = elTypes.has('object');

      if (isObjArr) {
        arrayMeta[path] = arrayMeta[path] || { elementFields: new Set(), correlated: true };
        for (const el of value) {
          if (el && typeof el === 'object' && !Array.isArray(el)) {
            for (const k of Object.keys(el)) {
              arrayMeta[path].elementFields.add(k);
              walk(el[k], `${path}.${k}`, true);
            }
          }
        }
      } else {
        for (const el of value) walk(el, path, true);
      }
      return;
    }

    if (t === 'object') {
      fields[path] = fields[path] || newField();
      fields[path].types.add('object');
      for (const [k, v] of Object.entries(value)) {
        walk(v, path ? `${path}.${k}` : k, inArray);
      }
      return;
    }

    fields[path] = fields[path] || newField();
    fields[path].types.add(t);
    if (inArray) fields[path].array = true;

    if (t === 'string' && fields[path].enums.size < 25 && value.length <= 30) {
      fields[path].enums.add(value);
    }
    fields[path].sampleCount++;
  }

  for (const doc of docs) {
    for (const [k, v] of Object.entries(doc)) walk(v, k, false);
  }

  function collectPaths(obj, prefix, seen) {
    if (obj === null || typeof obj !== 'object') return;
    if (Array.isArray(obj)) { for (const e of obj) collectPaths(e, prefix, seen); return; }
    if (obj instanceof ObjectId || obj instanceof Date ||
        obj instanceof Decimal128 || obj instanceof Long ||
        obj instanceof Binary || obj instanceof Timestamp) return;
    for (const [k, v] of Object.entries(obj)) {
      const p = prefix ? `${prefix}.${k}` : k;
      seen.add(p);
      collectPaths(v, p, seen);
    }
  }
  for (const doc of docs) {
    const seen = new Set();
    collectPaths(doc, '', seen);
    for (const p of seen) pathCounts[p] = (pathCounts[p] || 0) + 1;
  }

  const outFields = {};
  for (const [path, meta] of Object.entries(fields)) {
    if (meta.types.has('object') && meta.types.size === 1) continue;
    const type = pickType(meta.types);
    const entry = { type };
    if (meta.types.size > 1) entry.allTypes = [...meta.types].map(t => TYPE_ALIAS[t] || t);
    if (meta.array) entry.array = true;
    if (arrayMeta[path]?.correlated) entry.correlated = true;
    if ((pathCounts[path] || 0) < total) entry.optional = true;

    if (type === 'string' && meta.enums.size > 0 && meta.enums.size <= 15
        && meta.sampleCount >= total * 0.8) {
      entry.enum = [...meta.enums];
    }
    outFields[path] = entry;
  }

  const arrays = {};
  for (const [path, m] of Object.entries(arrayMeta)) {
    arrays[path] = { correlated: true, elementFields: [...m.elementFields] };
  }

  return {
    collection: null,
    documentCount: total,
    fields: outFields,
    arrays: Object.keys(arrays).length ? arrays : undefined,
  };
}

function pickType(types) {
  const order = ['objectId','date','decimal','long','binary','timestamp','regex',
                 'string','boolean','int','double','null','object'];
  for (const t of order) if (types.has(t)) return t;
  return [...types][0];
}

// ─────────────────────────────────────────────────────────────────────────────
// 5. COMPACT SCHEMA
// ─────────────────────────────────────────────────────────────────────────────
function toCompactSchema(schema, fieldFilter = null) {
  const out = { coll: schema.collection, f: {} };
  const arrayRoots = Object.keys(schema.arrays || {});
  const arrayGroups = {};
  const scalars = {};

  for (const [path, meta] of Object.entries(schema.fields)) {
    if (fieldFilter && !fieldFilter.has(path)) {
      const root = arrayRoots.find(r => path.startsWith(r + '.'));
      if (!root || !fieldFilter.has(root)) continue;
    }

    const t = TYPE_ALIAS[meta.type] || meta.type;
    const star = meta.optional ? '*' : '';
    const entry = meta.enum ? { enum: meta.enum } : t;

    const root = arrayRoots.find(r => path.startsWith(r + '.'));
    if (root && meta.array) {
      const sub = path.slice(root.length + 1);
      arrayGroups[root] = arrayGroups[root] || { sub: {}, corr: 1 };
      arrayGroups[root].sub[sub + star] = entry;
    } else if (meta.array) {
      scalars[path + star] = { '[]': entry };
    } else {
      scalars[path + star] = entry;
    }
  }

  Object.assign(out.f, scalars);
  for (const [root, g] of Object.entries(arrayGroups)) {
    out.f[root] = { '[]': g.sub };
    if (g.corr) out.f[root].corr = 1;
  }
  return out;
}

// ─────────────────────────────────────────────────────────────────────────────
// 6. FIELD RETRIEVAL (RAG-lite)
// ─────────────────────────────────────────────────────────────────────────────
function retrieveRelevantFields(schema, question, maxFields = 40) {
  const words = question.toLowerCase().split(/\W+/).filter(w => w.length > 2);
  const arrayRoots = Object.keys(schema.arrays || {});
  const scored = [];

  for (const [path, meta] of Object.entries(schema.fields)) {
    let score = 0;
    const lower = path.toLowerCase();
    for (const w of words) {
      if (lower.includes(w)) score += 5;
      if (lower.split('.').pop().includes(w)) score += 3;
    }
    if (meta.enum) {
      for (const v of meta.enum) {
        if (words.some(w => String(v).toLowerCase().includes(w))) score += 2;
      }
    }
    if (path === '_id') score += 100;
    scored.push({ path, score });
  }

  const selected = new Set();
  const sorted = scored.sort((a, b) => b.score - a.score);
  for (const { path, score } of sorted) {
    if (selected.size >= maxFields) break;
    if (score > 0 || selected.size < 15) {
      selected.add(path);
      const root = arrayRoots.find(r => path.startsWith(r + '.'));
      if (root) {
        selected.add(root);
        for (const p of Object.keys(schema.fields)) {
          if (p.startsWith(root + '.')) selected.add(p);
        }
      }
    }
  }
  selected.add('_id');
  return selected;
}

// ─────────────────────────────────────────────────────────────────────────────
// 7. PROMPT — MongoDB official best practices baked in
// ─────────────────────────────────────────────────────────────────────────────
const ALIAS_LEGEND = `ALIASES: oid=objectId str=string date=date dec=decimal lng=long int=int dbl=double bool=boolean bin=binary ts=timestamp rx=regex
CONVENTIONS:
  "f":"t"                    scalar
  "f":{"[]":"t"}             scalar array
  "f":{"[]":{...}}           array of objects
  "f":{"[]":{...},"corr":1}  correlated array -> MUST $unwind before correlating fields from it
  "*" suffix                 optional
  {"enum":[...]}             allowed values`;

const TYPE_RULES = `TYPE ENCODING (EJSON — match exactly):
  date -> {"$date":"ISO8601"}       e.g. {"$date":"2024-01-01T00:00:00Z"}
  oid  -> {"$oid":"24-hex"}         e.g. {"$oid":"507f1f77bcf86cd799439011"}
  dec  -> {"$numberDecimal":"str"}  e.g. {"$numberDecimal":"250.50"}
  lng  -> {"$numberLong":"str"}     e.g. {"$numberLong":"42"}
  bin  -> {"$binary":{"base64":"...","subType":"00"}}
  ts   -> {"$timestamp":{"t":n,"i":n}}`;

const MONGO_RULES = `MONGODB BEST-PRACTICES (follow strictly):
1.  Put $match as the FIRST stage to leverage indexes.
2.  Prefer exact match; use collation or /i regex only when required.
3.  Ensure proper use of MongoDB operators ($eq, $gt, $lt, etc.) and data types (ObjectId, ISODate)
4.  For complex queries, use aggregation pipeline with proper stages ($match, $group, $lookup, etc.)
5.  Consider performance by utilizing available indexes, avoiding $where and full collection scans, and using covered queries where possible
6.  Include sorting (.sort()) and limiting (.limit()), when appropriate, for result set management
7.  Handle null values and existence checks explicitly with $exists and $type operators to differentiate between missing fields, null values, and empty arrays
8.  Do not include 'null' in results objects in aggregation, e.g. do not include _id: null,
9.  For date operations, NEVER use an empty new date object (e.g. 'new Date()'). ALWAYS specify the date, such as 'new Date("2024-10-24")'. 
10. For Decimal128 operations, prefer range queries over exact equality
11. When querying arrays, use appropriate operators like $elemMatch for complex matching, $all to match multiple elements, or $size for array length checks
12. Put $sort immediately after the filtering $match, before $limit.
13. For top-N, always $sort before $limit — never $limit before $sort.
14. For distinct values, prefer {$group:{_id:"$field"}} over $addToSet.
15. Put $project at the END to shape output.
16. NEVER use: $where,$function,$accumulator,$out,$merge,$currentOp,
    $listSessions,$planCacheStats.
17. NEVER $lookup into "system.*" or other databases.
18. If no sort is required, use {_id:1} for stable pagination.
`;

function buildPrompt(compactSchema, question, context = {}) {
  const indexes = context.indexes?.length
    ? `INDEXES: ${JSON.stringify(context.indexes)}`
    : null;

  const parts = [
    'ROLE: You are an expert data analyst experienced at using MongoDB. our job is to take information about a MongoDB database plus a natural language query and generate a MongoDB shell (mongosh) query to execute to retrieve the information needed to answer the natural language query',
    'Format the mongosh query in the following structure:',
    '',
    'db.<collectionname>.find({/* query */})` or `db.<collectionname>.aggregate({/* query */})',
    '',
    ALIAS_LEGEND,
    '',
    TYPE_RULES,
    '',
    MONGO_RULES,
    '',
    `STAGES ALLOWED: ${[...ALLOWED_STAGES].join(', ')}`,
    '',
    `SCHEMA: ${JSON.stringify(compactSchema)}`,
  ];
  if (indexes) parts.push('', indexes);
  parts.push(
    '',
    `QUESTION: ${question}`,
    '',
    'OUTPUT REQUIREMENTS:',
    '- A JSON array of aggregation stages.',
    '- Use EJSON encoding for all BSON values.',
    '- No markdown fences, no prose, no explanation.',
    '- If the question cannot be answered with the given schema, return [].',
  );
  return parts.join('\n');
}

// ─────────────────────────────────────────────────────────────────────────────
// 8. GEMINI CALL (with optional context caching)
// ─────────────────────────────────────────────────────────────────────────────
let _genAI = null;
function genAI() {
  if (!CONFIG.geminiKey) throw new Error('GEMINI_API_KEY not set');
  if (!_genAI) _genAI = new GoogleGenerativeAI(CONFIG.geminiKey);
  return _genAI;
}

const cacheRegistry = new Map();  // key -> CachedContent

async function getCachedStatic(schema, compactSchema, indexes) {
  const key = `${CONFIG.geminiModel}::${schema.collection}::v3`;
  if (cacheRegistry.has(key)) return cacheRegistry.get(key);

  const staticPrompt = [
    'ROLE: You are a MongoDB query generator. Translate natural language into a',
    'single valid MongoDB aggregation pipeline. Return ONLY the pipeline.',
    '',
    ALIAS_LEGEND,
    '',
    TYPE_RULES,
    '',
    MONGO_RULES,
    '',
    `STAGES ALLOWED: ${[...ALLOWED_STAGES].join(', ')}`,
    '',
    `SCHEMA: ${JSON.stringify(compactSchema)}`,
    indexes?.length ? `INDEXES: ${JSON.stringify(indexes)}` : '',
  ].filter(Boolean).join('\n');

  const modelName = CONFIG.geminiModel.startsWith('models/')
    ? CONFIG.geminiModel
    : `models/${CONFIG.geminiModel}-001`;

  const cache = await genAI().cacheContent({
    model: modelName,
    contents: [{ role: 'user', parts: [{ text: staticPrompt }] }],
    ttlSeconds: 3600,
  });
  cacheRegistry.set(key, cache);
  return cache;
}

async function callGemini(prompt, compactSchema, schema, indexes) {
  const generationConfig = { temperature: 0.1, responseMimeType: 'application/json' };

  if (CONFIG.useCache && compactSchema && schema) {
    try {
      const cache = await getCachedStatic(schema, compactSchema, indexes);
      const model = genAI().getGenerativeModelFromCachedContent(cache, { generationConfig });
      const res = await model.generateContent(
        `QUESTION: ${prompt}\nOUTPUT:` // caller passes raw question in cache mode
      );
      return res.response.text();
    } catch (e) {
      console.error(`[cache] falling back: ${e.message}`);
    }
  }

  const model = genAI().getGenerativeModel({
    model: CONFIG.geminiModel,
    generationConfig,
  });
  const res = await model.generateContent(prompt);
  return res.response.text();
}

// ─────────────────────────────────────────────────────────────────────────────
// 9. EJSON PARSER
// ─────────────────────────────────────────────────────────────────────────────
function parseEJSON(v) {
  if (Array.isArray(v)) return v.map(parseEJSON);
  if (v && typeof v === 'object') {
    if ('$oid' in v)               return new ObjectId(v.$oid);
    if ('$date' in v)              return new Date(v.$date);
    if ('$numberDecimal' in v)     return Decimal128.fromString(v.$numberDecimal);
    if ('$numberLong' in v)        return Long.fromString(v.$numberLong);
    if ('$numberInt' in v)         return parseInt(v.$numberInt, 10);
    if ('$numberDouble' in v)      return parseFloat(v.$numberDouble);
    if ('$binary' in v)            return new Binary(
      Buffer.from(v.$binary.base64, 'base64'),
      v.$binary.subType ? parseInt(v.$binary.subType, 16) : 0);
    if ('$timestamp' in v)         return Timestamp.fromBits(v.$timestamp.i, v.$timestamp.t);
    if ('$regularExpression' in v) return new RegExp(
      v.$regularExpression.pattern, v.$regularExpression.options || '');
    const out = {};
    for (const [k, val] of Object.entries(v)) out[k] = parseEJSON(val);
    return out;
  }
  return v;
}

// ─────────────────────────────────────────────────────────────────────────────
// 10. VALIDATOR (mirrors prompt rules)
// ─────────────────────────────────────────────────────────────────────────────
const OPERATORS = new Set([
  '$sum','$avg','$min','$max','$first','$last','$push','$addToSet','$count',
  '$match','$project','$group','$sort','$limit','$skip','$unwind','$lookup',
  '$addFields','$facet','$bucket','$bucketAuto','$sample','$replaceRoot',
  '$replaceWith','$set','$unset','$redact',
  '$eq','$ne','$gt','$gte','$lt','$lte','$in','$nin','$and','$or','$not',
  '$nor','$exists','$type','$regex','$expr','$cond','$ifNull','$literal',
  '$arrayElemAt','$size','$filter','$map','$reduce','$concatArrays','$slice',
  '$toLower','$toUpper','$toString','$toInt','$toDouble','$toDecimal','$toDate',
  '$multiply','$divide','$add','$subtract','$concat','$substr','$substrBytes',
  '$substrCP','$let','$mergeObjects','$objectToArray','$arrayToObject',
  '$zip','$range','$switch','$mod','$abs','$ceil','$floor','$round',
  '$trim','$split','$strLenCP','$indexOfCP','$regexMatch','$regexFind',
  '$getField','$setField','$unsetField','$isNumber','$isArray',
  '$dateToString','$dateFromString','$year','$month','$dayOfMonth','$hour',
  '$minute','$second','$millisecond','$dayOfWeek','$dayOfYear','$week',
  '$stdDevPop','$stdDevSamp','$push','$sortArray','$maxN','$minN','$firstN',
  '$lastN','$topN','$bottomN','$binarySize','$bsonSize','$collStats',
]);

function validatePipeline(rawPipeline, schema) {
  const errors = [];
  const warnings = [];

  let pipeline;
  try {
    pipeline = typeof rawPipeline === 'string' ? JSON.parse(rawPipeline) : rawPipeline;
  } catch (e) {
    return { ok: false, errors: [`Invalid JSON: ${e.message}`], warnings };
  }
  if (!Array.isArray(pipeline)) {
    return { ok: false, errors: ['Pipeline must be a JSON array'], warnings };
  }

  // Stage whitelist / blacklist
  for (const stage of pipeline) {
    if (!stage || typeof stage !== 'object') {
      errors.push('Each stage must be an object'); continue;
    }
    for (const k of Object.keys(stage)) {
      if (!k.startsWith('$')) errors.push(`Invalid stage key "${k}"`);
      else if (BANNED_STAGES.has(k)) errors.push(`Banned stage: ${k}`);
      else if (!ALLOWED_STAGES.has(k)) errors.push(`Stage "${k}" not allowed`);
    }
  }

  // Banned operators anywhere in the pipeline
  (function scan(node) {
    if (Array.isArray(node)) return node.forEach(scan);
    if (node && typeof node === 'object') {
      for (const [k, v] of Object.entries(node)) {
        if (BANNED_STAGES.has(k)) errors.push(`Banned operator/stage inside pipeline: ${k}`);
        scan(v);
      }
    }
  })(pipeline);

  // $lookup safety
  for (const stage of pipeline) {
    if ('$lookup' in stage) {
      const from = stage.$lookup?.from || '';
      const crossDb = stage.$lookup?.db || '';
      if (from.startsWith('system.')) errors.push(`$lookup into system.* is forbidden: ${from}`);
      if (crossDb) warnings.push(`$lookup cross-database read: ${crossDb}.${from}`);
    }
  }

  // $limit-before-$sort rule
  {
    const limitIdx = pipeline.findIndex(s => '$limit' in s);
    const sortIdx  = pipeline.findIndex(s => '$sort' in s);
    if (limitIdx !== -1 && sortIdx !== -1 && limitIdx < sortIdx) {
      errors.push('$limit appears before $sort — non-deterministic for top-N.');
    }
  }

  // $match after $group warning
  {
    const matchIdxs = pipeline.map((s, i) => '$match' in s ? i : -1).filter(i => i >= 0);
    const groupIdxs = pipeline.map((s, i) => '$group' in s ? i : -1).filter(i => i >= 0);
    if (matchIdxs.length && groupIdxs.length && matchIdxs[0] > groupIdxs[0]) {
      warnings.push('$match after $group — consider earlier $match to use indexes.');
    }
  }

  // Field refs
  const knownPaths = new Set(Object.keys(schema.fields));
  const arrayRoots = Object.keys(schema.arrays || {});
  const refs = new Set();
  (function walk(node) {
    if (typeof node === 'string') {
      if (node.startsWith('$$')) return;
      if (node.startsWith('$')) {
        if (!OPERATORS.has(node)) refs.add(node.slice(1));
      }
      return;
    }
    if (Array.isArray(node)) return node.forEach(walk);
    if (node && typeof node === 'object') {
      for (const v of Object.values(node)) walk(v);
    }
  })(pipeline);

  for (const ref of refs) {
    if (!ref) continue;
    const root = ref.split('.')[0];
    const exists =
      knownPaths.has(ref) ||
      [...knownPaths].some(p => p.startsWith(ref + '.')) ||
      knownPaths.has(root) ||
      [...knownPaths].some(p => p.startsWith(root + '.'));
    if (!exists && !root.startsWith('_')) {
      warnings.push(`Unknown field path "${ref}" (may be computed).`);
    }
  }

  // Correlated array + $unwind
  const hasUnwind = pipeline.some(s => '$unwind' in s);
  for (const root of arrayRoots) {
    const used = [...refs].filter(r => r.startsWith(root + '.'));
    if (used.length >= 2 && !hasUnwind) {
      warnings.push(
        `Correlated array "${root}" has ${used.length} fields referenced without $unwind.`
      );
    }
  }

  // $expr misuse heuristic
  (function findBadExpr(node) {
    if (Array.isArray(node)) return node.forEach(findBadExpr);
    if (node && typeof node === 'object') {
      if ('$expr' in node) {
        const json = JSON.stringify(node.$expr);
        if (/\$(gt|gte|lt|lte|eq|ne)":\s*\[\s*"\$[^"]+",\s*[^"{]/.test(json)) {
          warnings.push('$expr used for field-vs-literal — plain operators are faster.');
        }
      }
      for (const v of Object.values(node)) findBadExpr(v);
    }
  })(pipeline);

  // Type encoding checks
  const datePaths = Object.entries(schema.fields).filter(([,m]) => m.type === 'date').map(([p]) => p);
  const decPaths  = Object.entries(schema.fields).filter(([,m]) => m.type === 'decimal').map(([p]) => p);
  const oidPaths  = Object.entries(schema.fields).filter(([,m]) => m.type === 'objectId').map(([p]) => p);

  (function walkPairs(node, ctx) {
    if (Array.isArray(node)) return node.forEach(n => walkPairs(n, ctx));
    if (node && typeof node === 'object') {
      for (const [k, v] of Object.entries(node)) {
        const path = `${ctx}.${k}`;
        if (v === null || typeof v !== 'object') {
          for (const dp of datePaths) {
            if (path.endsWith(dp) && typeof v === 'string' && !/^\$/.test(v)) {
              errors.push(`Date field "${dp}" compared with plain string "${v}". Use {"$date":...}.`);
            }
          }
          for (const op of oidPaths) {
            if (path.endsWith(op) && typeof v === 'string' && /^[0-9a-f]{24}$/i.test(v)) {
              errors.push(`ObjectId field "${op}" compared with plain string. Use {"$oid":...}.`);
            }
          }
          for (const decP of decPaths) {
            if (path.endsWith(decP) && typeof v === 'number') {
              warnings.push(`Decimal field "${decP}" compared with plain number. Prefer {"$numberDecimal":"${v}"}.`);
            }
          }
        } else {
          walkPairs(v, path);
        }
      }
    }
  })(pipeline, '');

  // Enum membership
  for (const [path, meta] of Object.entries(schema.fields)) {
    if (!meta.enum) continue;
    (function checkEnum(node, ctx) {
      if (Array.isArray(node)) return node.forEach(n => checkEnum(n, ctx));
      if (node && typeof node === 'object') {
        for (const [k, v] of Object.entries(node)) {
          const p = `${ctx}.${k}`;
          if (p.endsWith(path) && typeof v === 'string' && !meta.enum.includes(v)) {
            errors.push(`Field "${path}" value "${v}" not in enum [${meta.enum.join(', ')}].`);
          }
          checkEnum(v, p);
        }
      }
    })(pipeline, '');
  }

  return { ok: errors.length === 0, errors, warnings, pipeline };
}

// ─────────────────────────────────────────────────────────────────────────────
// 11. MAIN
// ─────────────────────────────────────────────────────────────────────────────
async function main() {
  const args = parseArgs(process.argv.slice(2));
  if (args.uri)        CONFIG.mongoUri   = args.uri;
  if (args.db)         CONFIG.dbName     = args.db;
  if (args.collection) CONFIG.collection = args.collection;
  if (args.maxFields)  CONFIG.maxFields  = parseInt(args.maxFields, 10);
  if (args.cache)      CONFIG.useCache   = true;

  const question = args.ask || 'Find 5 documents';

  const client = new MongoClient(CONFIG.mongoUri);
  await client.connect();

  try {
    console.log(`\n▶ Sampling ${CONFIG.dbName}.${CONFIG.collection}...`);
    const docs = await sampleCollection(client, CONFIG.dbName, CONFIG.collection, CONFIG.sampleSize);
    console.log(`  ✓ ${docs.length} docs`);

    console.log('▶ Fetching indexes...');
    const indexes = await fetchIndexes(client, CONFIG.dbName, CONFIG.collection);
    console.log(`  ✓ ${indexes.length} non-_id indexes`);

    console.log('▶ Inferring full schema...');
    const schema = inferFlattenedSchema(docs);
    schema.collection = `${CONFIG.dbName}.${CONFIG.collection}`;
    console.log(`  ✓ ${Object.keys(schema.fields).length} field paths`);

    console.log('▶ Retrieving relevant fields...');
    const relevant = retrieveRelevantFields(schema, question, CONFIG.maxFields);
    console.log(`  ✓ ${relevant.size} / ${Object.keys(schema.fields).length} fields kept`);

    console.log('▶ Building compact schema...');
    const compact = toCompactSchema(schema, relevant);
    console.log(`  ✓ Compact JSON: ${JSON.stringify(compact).length} chars (~${Math.ceil(JSON.stringify(compact).length / 4)} tokens)`);

    console.log('▶ Building prompt...');
    const prompt = buildPrompt(compact, question, { indexes });
    console.log(`  ✓ Prompt: ${prompt.length} chars (~${Math.ceil(prompt.length / 4)} tokens)`);

    if (args.showPrompt) console.log('\n--- PROMPT ---\n' + prompt + '\n');

    if (!CONFIG.geminiKey) {
      console.log('(GEMINI_API_KEY not set — prompt above for manual use)');
      return;
    }

    console.log('▶ Calling Gemini...');
    const raw = CONFIG.useCache
      ? await callGemini(question, compact, schema, indexes)
      : await callGemini(prompt, null, null, null);
    console.log('\n--- RAW OUTPUT ---\n' + raw);

    console.log('▶ Validating...');
    const v = validatePipeline(raw, schema);
    v.warnings.forEach(w => console.log(`  ⚠ ${w}`));
    if (!v.ok) {
      v.errors.forEach(e => console.log(`  ✗ ${e}`));
      process.exit(2);
    }
    console.log('  ✓ Valid');

    console.log('▶ Executing...');
    const bsonPipeline = parseEJSON(v.pipeline);
    const results = await client.db(CONFIG.dbName)
      .collection(CONFIG.collection)
      .aggregate(bsonPipeline)
      .limit(CONFIG.resultLimit)
      .toArray();

    console.log(`  ✓ ${results.length} results`);
    console.log('\n--- PIPELINE ---\n' + JSON.stringify(v.pipeline, null, 2));
    console.log('\n--- RESULTS ---\n' + JSON.stringify(results.slice(0, 10), null, 2));

  } finally {
    await client.close();
  }
}

function parseArgs(argv) {
  const out = {};
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a.startsWith('--')) {
      const key = a.slice(2);
      const val = argv[i + 1] && !argv[i + 1].startsWith('--') ? argv[++i] : true;
      out[key] = val;
    }
  }
  return out;
}

// ─────────────────────────────────────────────────────────────────────────────
// EXPORTS
// ─────────────────────────────────────────────────────────────────────────────
module.exports = {
  sampleCollection, fetchIndexes,
  inferFlattenedSchema, toCompactSchema, retrieveRelevantFields,
  buildPrompt, callGemini, parseEJSON, validatePipeline,
  CONFIG, ALLOWED_STAGES, BANNED_STAGES,
  TYPE_ALIAS, ALIAS_TO_TYPE,
  ALIAS_LEGEND, TYPE_RULES, MONGO_RULES,
};

if (require.main === module) {
  main().catch(e => { console.error(e); process.exit(1); });
}