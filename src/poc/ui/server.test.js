'use strict';

const test = require('node:test');
const assert = require('node:assert');
const { createServer } = require('./server.js');

async function withServer(t) {
  const server = createServer();
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  t.after(() => server.close());
  return `http://127.0.0.1:${server.address().port}`;
}

test('GET /api/health reports status and readiness', async (t) => {
  const base = await withServer(t);
  const res = await fetch(`${base}/api/health`);
  assert.strictEqual(res.status, 200);
  const body = await res.json();
  assert.strictEqual(body.status, 'ok');
  assert.strictEqual(typeof body.liveReady, 'boolean');
  assert.strictEqual(typeof body.coreLoaded, 'boolean');
});

test('POST /api/ask returns a well-formed response', async (t) => {
  const base = await withServer(t);
  const res = await fetch(`${base}/api/ask`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ db: 'shop', collection: 'users', question: 'top 5 users by amount' }),
  });
  assert.strictEqual(res.status, 200);
  const body = await res.json();
  assert.ok(body.mode === 'demo' || body.mode === 'live');
  assert.ok(Array.isArray(body.pipeline));
  assert.ok(Array.isArray(body.results));
  assert.ok(Array.isArray(body.warnings));
  assert.ok(Array.isArray(body.errors));
  assert.ok(body.schema && body.schema.flattened && body.schema.compact);
});

test('POST /api/ask rejects an empty question', async (t) => {
  const base = await withServer(t);
  const res = await fetch(`${base}/api/ask`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ question: '   ' }),
  });
  assert.strictEqual(res.status, 400);
  const body = await res.json();
  assert.match(body.error, /question is required/);
});

test('unknown route returns 404', async (t) => {
  const base = await withServer(t);
  const res = await fetch(`${base}/nope`);
  assert.strictEqual(res.status, 404);
});
