import type {
  DebugStats,
  ExceptionEntry,
  ListQuery,
  LogEntry,
  PagedResult,
  RequestEntry,
  SqlQueryEntry,
} from './types';

declare global {
  interface Window {
    __DEBUG_BASE_PATH__?: string;
  }
}

const raw = window.__DEBUG_BASE_PATH__ ?? '';
// During `vite dev` the token isn't replaced; fall back to the default path.
export const basePath = raw.startsWith('__') ? '/_debug' : raw;
const apiBase = `${basePath}/api`;

async function getJson<T>(url: string, signal?: AbortSignal): Promise<T> {
  const res = await fetch(url, { signal });
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
  return res.json();
}

function listUrl(resource: string, q: ListQuery): string {
  const p = new URLSearchParams();
  p.set('page', String(q.page));
  p.set('pageSize', String(q.pageSize));
  if (q.search) p.set('search', q.search);
  if (q.sortBy) p.set('sortBy', q.sortBy);
  if (q.sortDescending !== undefined) p.set('sortDescending', String(q.sortDescending));
  if (q.method) p.set('method', q.method);
  if (q.statusCode) p.set('statusCode', String(q.statusCode));
  if (q.level) p.set('level', q.level);
  return `${apiBase}/${resource}?${p}`;
}

export const api = {
  stats: (signal?: AbortSignal) => getJson<DebugStats>(`${apiBase}/stats`, signal),

  requests: (q: ListQuery, signal?: AbortSignal) =>
    getJson<PagedResult<RequestEntry>>(listUrl('requests', q), signal),
  request: (id: string) => getJson<RequestEntry>(`${apiBase}/requests/${id}`),

  queries: (q: ListQuery, signal?: AbortSignal) =>
    getJson<PagedResult<SqlQueryEntry>>(listUrl('queries', q), signal),
  query: (id: string) => getJson<SqlQueryEntry>(`${apiBase}/queries/${id}`),

  logs: (q: ListQuery, signal?: AbortSignal) =>
    getJson<PagedResult<LogEntry>>(listUrl('logs', q), signal),
  log: (id: string) => getJson<LogEntry>(`${apiBase}/logs/${id}`),

  exceptions: (q: ListQuery, signal?: AbortSignal) =>
    getJson<PagedResult<ExceptionEntry>>(listUrl('exceptions', q), signal),
  exception: (id: string) => getJson<ExceptionEntry>(`${apiBase}/exceptions/${id}`),

  clearAll: () => fetch(`${apiBase}/clear`, { method: 'DELETE' }),
  exportUrl: `${apiBase}/export`,
};
