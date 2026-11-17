import type { RequestEntry } from './types';
import { fmtDuration } from './format';

export const REPLAY_HEADER = 'X-Debug-Replay-Of';

export interface ReplayDraft {
  method: string;
  path: string;
  headerText: string;
  body: string;
  bodyDisabledReason: string | null;
  redactedCount: number;
  // origin the request was captured on, when it isn't the dashboard's own
  originMismatch: string | null;
}

export interface ParsedHeaders {
  headers: [string, string][];
  dropped: string[];
  errors: string[];
}

// fetch() silently ignores these (https://fetch.spec.whatwg.org/#forbidden-request-header)
const FORBIDDEN = new Set([
  'accept-charset', 'accept-encoding', 'access-control-request-headers', 'access-control-request-method',
  'access-control-request-private-network', 'connection', 'content-length', 'cookie', 'cookie2', 'date',
  'dnt', 'expect', 'host', 'keep-alive', 'origin', 'referer', 'set-cookie', 'te', 'trailer',
  'transfer-encoding', 'upgrade', 'via',
]);
const METHOD_OVERRIDES = new Set(['x-http-method', 'x-http-method-override', 'x-method-override']);
const FORBIDDEN_METHODS = new Set(['connect', 'trace', 'track']);

const TOKEN = /^[!#$%&'*+.^_`|~0-9A-Za-z-]+$/;
// Headers only takes ISO-8859-1 and throws on anything else
const BAD_VALUE = /[\0\r\nĀ-￿]/;

function forbidden(name: string, value: string): boolean {
  const n = name.toLowerCase();
  if (FORBIDDEN.has(n) || n.startsWith('sec-') || n.startsWith('proxy-')) return true;
  return METHOD_OVERRIDES.has(n) && value.split(',').some((m) => FORBIDDEN_METHODS.has(m.trim().toLowerCase()));
}

export function parseHeaderLines(text: string): ParsedHeaders {
  const out: ParsedHeaders = { headers: [], dropped: [], errors: [] };

  text.split(/\r?\n/).forEach((line, i) => {
    if (!line.trim()) return;
    const colon = line.indexOf(':');
    const name = colon < 0 ? '' : line.slice(0, colon).trim();
    const value = colon < 0 ? '' : line.slice(colon + 1).trim();

    if (!name) {
      out.errors.push(`line ${i + 1}: expected "Name: value"`);
    } else if (!TOKEN.test(name)) {
      out.errors.push(`"${name}" isn't a valid header name`);
    } else if (BAD_VALUE.test(value)) {
      out.errors.push(`${name} has characters a header value can't carry`);
    } else if (forbidden(name, value)) {
      out.dropped.push(name);
    } else {
      out.headers.push([name, value]);
    }
  });

  return out;
}

export function countRedacted(...texts: string[]): number {
  return texts.reduce((n, t) => n + (t.match(/\*\*\*/g)?.length ?? 0), 0);
}

function isText(contentType: string): boolean {
  const t = contentType.split(';')[0].trim().toLowerCase();
  return t.startsWith('text/')
    || t === 'application/x-www-form-urlencoded'
    || /^application\/(.+\+)?(json|xml)$/.test(t)
    || ['application/javascript', 'application/graphql', 'application/x-ndjson'].includes(t);
}

function bodyReason(e: RequestEntry): string | null {
  const body = e.requestBody ?? '';
  const type = e.contentType ?? '';
  if (/^multipart\//i.test(type)) return "Multipart bodies can't be rebuilt from the captured text.";
  if (type && body && !isText(type)) return `${type.split(';')[0]} bodies don't survive as text.`;
  if (body.startsWith('[Body too large')) return 'The body was too large to capture (over MaxBodySize).';
  if (body.startsWith('[Error reading request body')) return "The body couldn't be read when it was captured.";
  if (!body && e.requestSize > 0) return "The body wasn't captured (LogRequestBodies is off).";
  return null;
}

export function buildDraft(e: RequestEntry, origin: string = window.location.origin): ReplayDraft {
  const headerText = Object.entries(e.headers ?? {})
    .filter(([name]) => name.toLowerCase() !== REPLAY_HEADER.toLowerCase())
    .map(([name, value]) => `${name}: ${value}`)
    .join('\n');

  const bodyDisabledReason = bodyReason(e);
  const body = bodyDisabledReason ? '' : e.requestBody ?? '';
  const path = (e.path || '/') + (e.queryString ?? '');

  let captured: string | null = null;
  try {
    captured = new URL(e.url).origin;
  } catch {
    // relative or empty url, nothing to compare
  }

  return {
    method: e.method.toUpperCase(),
    path,
    headerText,
    body,
    bodyDisabledReason,
    redactedCount: countRedacted(path, headerText, body),
    originMismatch: captured && captured !== origin ? captured : null,
  };
}

// Absolute URL for the replay, or null when the path would leave the dashboard's origin.
// Browsers read "\" as "/" and drop tabs and newlines, so "/\evil.com" or "/\n/evil.com"
// would turn into "//evil.com"; reject those outright. The result is absolute on purpose:
// a pathname alone can still start with "//" (from "/.//evil.com") and re-resolve elsewhere.
export function replayUrl(path: string, origin: string = window.location.origin): string | null {
  if (/[\\\u0000-\u001f\u007f]/.test(path)) return null;
  let u: URL;
  try {
    u = new URL(path.trim(), origin);
  } catch {
    return null;
  }
  if (u.origin !== origin) return null;
  return u.origin + u.pathname + u.search;
}

export function sendsBody(method: string): boolean {
  const m = method.toUpperCase();
  return m !== 'GET' && m !== 'HEAD';
}

export function toFetchInit(d: ReplayDraft, sourceId: string): RequestInit {
  const headers = new Headers();
  for (const [name, value] of parseHeaderLines(d.headerText).headers) headers.append(name, value);
  headers.set(REPLAY_HEADER, sourceId);

  return {
    method: d.method.toUpperCase(),
    headers,
    body: sendsBody(d.method) && !d.bodyDisabledReason && d.body ? d.body : undefined,
    credentials: 'same-origin',
    redirect: 'manual',
  };
}

export type Tone = 'better' | 'worse' | 'same' | 'changed';

export interface CompareRow {
  label: string;
  before: number;
  after: number;
  delta: string;
  tone: Tone;
}

function countRow(label: string, before: number, after: number): CompareRow {
  const d = after - before;
  return {
    label,
    before,
    after,
    delta: d === 0 ? '0' : d > 0 ? `+${d}` : String(d),
    tone: d === 0 ? 'same' : d < 0 ? 'better' : 'worse',
  };
}

export function compareRows(before: RequestEntry, after: RequestEntry): CompareRow[] {
  const ok = (s: number) => s < 400;
  const status: Tone = before.statusCode === after.statusCode
    ? 'same'
    : ok(before.statusCode) && !ok(after.statusCode)
      ? 'worse'
      : !ok(before.statusCode) && ok(after.statusCode) ? 'better' : 'changed';

  const dur = (e: RequestEntry) => e.durationMs ?? e.executionTimeMs;
  const ms = dur(after) - dur(before);
  // a few ms either way is noise, not a change
  const noise = Math.max(5, dur(before) * 0.1);

  return [
    {
      label: 'Status',
      before: before.statusCode,
      after: after.statusCode,
      delta: status === 'same' ? '' : 'changed',
      tone: status,
    },
    {
      label: 'Duration',
      before: dur(before),
      after: dur(after),
      delta: ms === 0 ? '0 ms' : `${ms > 0 ? '+' : '-'}${fmtDuration(Math.abs(ms))}`,
      tone: Math.abs(ms) < noise ? 'same' : ms < 0 ? 'better' : 'worse',
    },
    countRow('SQL queries', before.sqlQueries?.length ?? 0, after.sqlQueries?.length ?? 0),
    countRow('Outgoing calls', before.outgoingHttp?.length ?? 0, after.outgoingHttp?.length ?? 0),
  ];
}
