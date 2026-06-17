declare global { interface Window { __FLAGS_BASE__?: string; } }
const raw = window.__FLAGS_BASE__ ?? '';
export const basePath = raw.startsWith('%') ? '/_flags' : raw;
const apiBase = `${basePath}/api`;

export interface Flag { name: string; enabled: boolean; description?: string | null; firstSeen: string; updatedAt: string; }

export const api = {
  all: (signal?: AbortSignal) => fetch(`${apiBase}/flags`, { signal }).then(r => { if (!r.ok) throw new Error(`${r.status}`); return r.json() as Promise<Flag[]>; }),
  set: (name: string, enabled: boolean) =>
    fetch(`${apiBase}/flags/${encodeURIComponent(name)}`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ enabled }),
    }),
  remove: (name: string) =>
    fetch(`${apiBase}/flags/${encodeURIComponent(name)}`, { method: 'DELETE' }),
};
