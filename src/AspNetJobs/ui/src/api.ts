declare global { interface Window { __JOBS_BASE__?: string; } }
const raw = window.__JOBS_BASE__ ?? '';
export const basePath = raw.startsWith('%') ? '/_jobs' : raw;
const apiBase = `${basePath}/api`;

export type JobStatus = 'Pending' | 'Running' | 'Succeeded' | 'Failed';

export interface Job {
  jobId: string;
  name: string;
  status: JobStatus;
  enqueuedAt: string;
  startedAt?: string | null;
  finishedAt?: string | null;
  durationMs?: number | null;
  error?: string | null;
}

export const api = {
  list: (signal?: AbortSignal) =>
    fetch(`${apiBase}/jobs`, { signal }).then(r => { if (!r.ok) throw new Error(`${r.status}`); return r.json() as Promise<Job[]>; }),
  clear: () => fetch(`${apiBase}/clear`, { method: 'DELETE' }),
};
