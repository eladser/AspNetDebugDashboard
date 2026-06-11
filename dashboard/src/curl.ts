import type { RequestEntry } from './types';

// Headers the server adds on its own, or that won't replay meaningfully.
const SKIP = new Set(['host', 'content-length', 'connection', 'accept-encoding']);

const q = (s: string) => `'${s.replace(/'/g, `'\\''`)}'`;

export function toCurl(r: RequestEntry): string {
  const parts = [`curl -X ${r.method.toUpperCase()} ${q(r.url)}`];

  for (const [name, value] of Object.entries(r.headers ?? {})) {
    if (SKIP.has(name.toLowerCase())) continue;
    parts.push(`  -H ${q(`${name}: ${value}`)}`);
  }

  if (r.requestBody) {
    parts.push(`  -d ${q(r.requestBody)}`);
  }

  return parts.join(' \\\n');
}
