import { readFileSync } from 'node:fs';

const html = readFileSync(new URL('./index.html', import.meta.url), 'utf8');
const emoji = /\p{Extended_Pictographic}/u;

const checks = [
  ['emoji', emoji.test(html)],
  ['cite', html.includes('[cite')],
  ['todo', /\b(TODO|TBD)\b/.test(html)],
  ['placeholder', /\{\{[^}]+\}\}/.test(html)],
  ['external-asset', /(?:src|href)\s*=\s*["']https?:/i.test(html)]
];

const failed = checks.filter(([, bad]) => bad).map(([name]) => name);
if (failed.length) {
  console.error('FAIL:', failed.join(', '));
  process.exit(1);
}

if (!html.includes('Built today') || !html.includes('Coming next')) {
  console.error('FAIL: status pills missing');
  process.exit(1);
}

if (!html.includes('Multi-Agent MongoDB NLP')) {
  console.error('FAIL: product name missing');
  process.exit(1);
}

console.log('showcase checks passed');
