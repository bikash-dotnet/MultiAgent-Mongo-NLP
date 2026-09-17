import { readFileSync, readdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));
const files = readdirSync(here).filter((f) => f.endsWith('.html'));

if (files.length === 0) {
  console.error('FAIL: no showcase HTML files found');
  process.exit(1);
}

const emoji = /\p{Extended_Pictographic}/u;
let failed = false;

for (const file of files) {
  const html = readFileSync(join(here, file), 'utf8');
  const checks = [
    ['emoji', emoji.test(html)],
    ['cite', html.includes('[cite')],
    ['todo', /\b(TODO|TBD)\b/.test(html)],
    ['placeholder', /\{\{[^}]+\}\}/.test(html)],
    ['external-asset', /(?:src|href)\s*=\s*["']https?:/i.test(html)]
  ];
  const bad = checks.filter(([, isBad]) => isBad).map(([name]) => name);
  if (bad.length) {
    console.error('FAIL ' + file + ':', bad.join(', '));
    failed = true;
  }
  if (!html.includes('Multi-Agent MongoDB NLP')) {
    console.error('FAIL ' + file + ': product name missing');
    failed = true;
  }
}

const combined = files.map((f) => readFileSync(join(here, f), 'utf8')).join('\n');
if (!combined.includes('Built today') || !combined.includes('Coming next')) {
  console.error('FAIL: status pills missing in the executive story');
  failed = true;
}

if (failed) {
  process.exit(1);
}

console.log('showcase checks passed (' + files.join(', ') + ')');
