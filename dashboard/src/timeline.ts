import type { RequestEntry, SqlQueryEntry } from './types';
import { fmtDuration } from './format';

export type Lane = 'sql' | 'http' | 'log' | 'dump' | 'exception';
export type Interval = [number, number];

export interface Row {
  id: string;
  lane: Lane;
  offset: number;
  dur: number;
  label: string;
  title: string;
  warn: boolean;
  nPlusOne?: number;
}

export interface Breakdown {
  sqlMs: number;
  httpMs: number;
  appMs: number;
  totalMs: number;
  parallel: boolean;
}

const hasOff = (e: { startOffsetMs?: number | null }): e is { startOffsetMs: number } =>
  typeof e.startOffsetMs === 'number';

const cut = (s: string, n: number) => (s.length > n ? `${s.slice(0, n)}…` : s);
const clamp = (v: number, lo: number, hi: number) => Math.min(hi, Math.max(lo, v));

export function totalOf(r: RequestEntry): number {
  return r.durationMs ?? r.executionTimeMs ?? 0;
}

export function hasTimeline(r: RequestEntry): boolean {
  if (totalOf(r) <= 0) return false;
  return [r.sqlQueries, r.outgoingHttp, r.logs, r.dumps].some((l) => (l ?? []).some(hasOff));
}

export function unionMs(intervals: Interval[]): number {
  const sorted = [...intervals].sort((a, b) => a[0] - b[0]);
  let sum = 0;
  let end = -Infinity;
  for (const [s, e] of sorted) {
    if (e <= end) continue;
    sum += e - Math.max(s, end);
    end = e;
  }
  return sum;
}

export function clampPct(offset: number, duration: number, total: number): { left: number; width: number } {
  if (total <= 0) return { left: 0, width: 0 };
  const s = clamp(offset, 0, total);
  const e = clamp(offset + duration, 0, total);
  return { left: (s * 100) / total, width: ((e - s) * 100) / total };
}

export function nPlusOneGroups(queries: SqlQueryEntry[]): { query: string; count: number }[] {
  const counts = new Map<string, number>();
  for (const q of queries) counts.set(q.query, (counts.get(q.query) ?? 0) + 1);
  return [...counts]
    .filter(([, count]) => count >= 3)
    .map(([query, count]) => ({ query, count }))
    .sort((a, b) => b.count - a.count);
}

function lanes(r: RequestEntry) {
  const total = totalOf(r);
  const iv = (e: { startOffsetMs?: number | null; durationMs?: number | null; executionTimeMs: number }): Interval[] => {
    if (!hasOff(e)) return [];
    const s = clamp(e.startOffsetMs, 0, total);
    return [[s, clamp(e.startOffsetMs + (e.durationMs ?? e.executionTimeMs), 0, total)]];
  };
  return {
    sql: (r.sqlQueries ?? []).flatMap(iv),
    http: (r.outgoingHttp ?? []).flatMap(iv),
  };
}

export function breakdown(r: RequestEntry): Breakdown {
  const totalMs = totalOf(r);
  const { sql, http } = lanes(r);
  const sqlMs = unionMs(sql);
  const httpMs = unionMs(http);
  const both = unionMs([...sql, ...http]);
  return {
    sqlMs,
    httpMs,
    appMs: Math.max(0, totalMs - both),
    totalMs,
    parallel: sqlMs + httpMs - both > 0.005,
  };
}

function excType(text: string): string {
  const m = /^[^:\r\n]+/.exec(text.trim());
  return m ? m[0] : 'Exception';
}

export function buildRows(r: RequestEntry): Row[] {
  const total = totalOf(r);
  const rows: Row[] = [];
  const groups = new Map(nPlusOneGroups(r.sqlQueries ?? []).map((g) => [g.query, g.count]));

  for (const q of r.sqlQueries ?? []) {
    if (!hasOff(q)) continue;
    const dur = q.durationMs ?? q.executionTimeMs;
    rows.push({
      id: q.id, lane: 'sql', offset: q.startOffsetMs, dur,
      label: cut(q.query.replace(/\s+/g, ' '), 80),
      title: cut(q.query, 200),
      warn: q.isSlowQuery || groups.has(q.query),
    });
  }
  for (const h of r.outgoingHttp ?? []) {
    if (!hasOff(h)) continue;
    const dur = h.durationMs ?? h.executionTimeMs;
    const st = h.statusCode ? String(h.statusCode) : 'failed';
    rows.push({
      id: h.id, lane: 'http', offset: h.startOffsetMs, dur,
      label: `${h.method} ${h.url}`,
      title: `${h.method} ${h.url} → ${st}\nDuration runs to the response headers, not the body`,
      warn: false,
    });
  }
  for (const l of r.logs ?? []) {
    if (!hasOff(l)) continue;
    rows.push({ id: l.id, lane: 'log', offset: l.startOffsetMs, dur: 0, label: l.level, title: cut(l.message, 200), warn: false });
  }
  for (const d of r.dumps ?? []) {
    if (!hasOff(d)) continue;
    const name = d.label ?? d.typeName;
    rows.push({ id: d.id, lane: 'dump', offset: d.startOffsetMs, dur: 0, label: name, title: name, warn: false });
  }

  rows.sort((a, b) => a.offset - b.offset);

  const seen = new Set<string>();
  const byId = new Map((r.sqlQueries ?? []).map((q) => [q.id, q.query]));
  for (const row of rows) {
    const text = row.lane === 'sql' ? byId.get(row.id) : undefined;
    if (text === undefined || !groups.has(text) || seen.has(text)) continue;
    seen.add(text);
    row.nPlusOne = groups.get(text);
  }

  if (r.exception) {
    const t = excType(r.exception);
    rows.push({ id: 'exception', lane: 'exception', offset: total, dur: 0, label: t, title: t, warn: false });
  }
  return rows;
}

// fmtDuration shows anything under 1 ms as "<1 ms", so on a short request every tick
// and every lane total would read the same
export function fmtMs(ms: number): string {
  return ms < 10 ? `${+ms.toFixed(2)} ms` : fmtDuration(ms);
}

// Labels go on the roomier side of their bar, or inside a long bar that leaves too
// little room on either side (an outgoing call that takes most of the request).
export function labelSide(left: number, width: number): 'left' | 'right' | 'in' {
  const right = 100 - left - width;
  const room = Math.max(left, right);
  if (room < 20 && width > room) return 'in';
  return left > right ? 'left' : 'right';
}

export function tickValues(total: number): number[] {
  if (total <= 0) return [0];
  let step = 1;
  for (let k = -3; k < 12; k++) {
    const p = 10 ** k;
    const c = [1, 2, 2.5, 5].map((m) => m * p).find((s) => Math.floor(total / s) + 1 <= 6);
    if (c) { step = c; break; }
  }
  const out: number[] = [];
  for (let i = 0; i * step <= total; i++) out.push(Math.round(i * step * 1000) / 1000);
  return out;
}
