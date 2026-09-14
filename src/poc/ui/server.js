'use strict';

const http = require('http');
const fs = require('fs');
const path = require('path');

const demo = require('./demo-data.js');

const PORT = parseInt(process.env.PORT || '8787', 10);
const HOST = process.env.HOST || '0.0.0.0';
const MAX_BODY = 100 * 1024;

let core = null;
let coreError = null;
let lastLiveError = null;
try {
  core = require('../mongo-ai-query.js');
} catch (e) {
  coreError = e.message;
}

function redactUri(uri) {
  return String(uri || '').replace(/\/\/[^@/]*@/, '//***@');
}

function modelLooksValid(name) {
  return /^(models\/)?gemini[-\w.]*$/i.test(String(name || ''));
}

const STATIC_FILES = {
  '/': 'index.html',
  '/index.html': 'index.html',
  '/app.js': 'app.js',
  '/styles.css': 'styles.css',
};

const CONTENT_TYPES = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
};

function sendJson(res, status, payload) {
  const body = JSON.stringify(payload, null, 2);
  res.writeHead(status, {
    'Content-Type': 'application/json; charset=utf-8',
    'Content-Length': Buffer.byteLength(body),
    'Cache-Control': 'no-store',
  });
  res.end(body);
}

function sendFile(res, fileName) {
  const full = path.join(__dirname, fileName);
  fs.readFile(full, (err, data) => {
    if (err) {
      sendJson(res, 500, { error: `Cannot read ${fileName}` });
      return;
    }
    res.writeHead(200, {
      'Content-Type': CONTENT_TYPES[path.extname(fileName)] || 'application/octet-stream',
      'Content-Length': data.length,
    });
    res.end(data);
  });
}

function readBody(req) {
  return new Promise((resolve, reject) => {
    let size = 0;
    const chunks = [];
    req.on('data', (chunk) => {
      size += chunk.length;
      if (size > MAX_BODY) {
        reject(new Error('Request body too large'));
        req.destroy();
        return;
      }
      chunks.push(chunk);
    });
    req.on('end', () => resolve(Buffer.concat(chunks).toString('utf8')));
    req.on('error', reject);
  });
}

function demoResponse(question, db, collection, note) {
  return {
    mode: 'demo',
    question,
    db: db || 'shop',
    collection: collection || 'users',
    schema: { flattened: demo.flattenedSchema, compact: demo.compactSchema },
    pipeline: demo.pipeline,
    warnings: demo.warnings,
    errors: [],
    results: demo.results,
    note,
  };
}

async function runLive(db, collection, question) {
  const { MongoClient } = require('mongodb');
  const client = new MongoClient(core.CONFIG.mongoUri, { serverSelectionTimeoutMS: 2500 });
  await client.connect();
  try {
    const docs = await core.sampleCollection(client, db, collection, core.CONFIG.sampleSize);
    const indexes = await core.fetchIndexes(client, db, collection);
    const schema = core.inferFlattenedSchema(docs);
    schema.collection = `${db}.${collection}`;

    const relevant = core.retrieveRelevantFields(schema, question, core.CONFIG.maxFields);
    const compact = core.toCompactSchema(schema, relevant);
    const prompt = core.buildPrompt(compact, question, { indexes });

    const raw = core.CONFIG.useCache
      ? await core.callGemini(question, compact, schema, indexes)
      : await core.callGemini(prompt, null, null, null);

    const validation = core.validatePipeline(raw, schema);
    let results = [];
    if (validation.ok) {
      results = await client
        .db(db)
        .collection(collection)
        .aggregate(core.parseEJSON(validation.pipeline))
        .limit(core.CONFIG.resultLimit)
        .toArray();
    }

    return {
      mode: 'live',
      question,
      db,
      collection,
      schema: { flattened: schema, compact },
      prompt,
      pipeline: validation.pipeline,
      warnings: validation.warnings,
      errors: validation.errors,
      results,
    };
  } finally {
    await client.close();
  }
}

function liveReady() {
  return Boolean(core && core.CONFIG.geminiKey);
}

function createServer() {
  return http.createServer(async (req, res) => {
    let url;
    try {
      url = new URL(req.url, 'http://localhost');
    } catch {
      sendJson(res, 400, { error: 'Invalid URL' });
      return;
    }

    if (req.method === 'GET' && url.pathname === '/api/health') {
      sendJson(res, 200, {
        status: 'ok',
        liveReady: liveReady(),
        coreLoaded: Boolean(core),
        coreError,
        lastLiveError,
        port: PORT,
      });
      return;
    }

    if (req.method === 'GET' && STATIC_FILES[url.pathname]) {
      sendFile(res, STATIC_FILES[url.pathname]);
      return;
    }

    if (req.method === 'POST' && url.pathname === '/api/ask') {
      let payload;
      try {
        const raw = await readBody(req);
        payload = raw ? JSON.parse(raw) : {};
      } catch (e) {
        sendJson(res, 400, { error: `Invalid request: ${e.message}` });
        return;
      }

      const question = String(payload.question || '').trim();
      const db = String(payload.db || 'shop').trim() || 'shop';
      const collection = String(payload.collection || 'users').trim() || 'users';

      if (!question) {
        sendJson(res, 400, { error: 'question is required' });
        return;
      }
      if (question.length > 500) {
        sendJson(res, 400, { error: 'question must be 500 characters or fewer' });
        return;
      }

      if (!liveReady()) {
        const note = coreError
          ? `Live mode unavailable (${coreError}). Run npm install and set GEMINI_API_KEY.`
          : 'GEMINI_API_KEY is not set. Showing demo data.';
        sendJson(res, 200, demoResponse(question, db, collection, note));
        return;
      }

      try {
        const result = await runLive(db, collection, question);
        lastLiveError = null;
        sendJson(res, 200, result);
      } catch (e) {
        lastLiveError = e.message;
        console.error(`[live] ${db}.${collection} failed: ${e.message}`);
        sendJson(res, 200, demoResponse(question, db, collection, `Live run failed (${e.message}). Showing demo data.`));
      }
      return;
    }

    sendJson(res, 404, { error: 'Not found' });
  });
}

if (require.main === module) {
  createServer().listen(PORT, HOST, () => {
    console.log(`mongo-ai-query UI listening on http://${HOST}:${PORT}`);
    if (!core) {
      console.warn(`Mode: demo (core unavailable: ${coreError})`);
      console.warn('Run "npm install" inside src/poc to enable live mode.');
      return;
    }
    if (!liveReady()) {
      console.warn('Mode: demo (GEMINI_API_KEY not set)');
      console.warn('Set GEMINI_API_KEY to enable live mode.');
      return;
    }
    console.log('Mode: live');
    console.log(`Model: ${core.CONFIG.geminiModel}`);
    console.log(`MongoDB: ${redactUri(core.CONFIG.mongoUri)}`);
    if (!modelLooksValid(core.CONFIG.geminiModel)) {
      console.warn(`WARNING: "${core.CONFIG.geminiModel}" does not look like a Gemini model. Use a name such as "gemini-1.5-flash".`);
    }
    console.warn('Live requests still fail if MongoDB is unreachable or the API key is invalid; watch for [live] errors below.');
  });
}

module.exports = { createServer, demoResponse, liveReady };
