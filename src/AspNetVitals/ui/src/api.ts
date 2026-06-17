declare global { interface Window { __VITALS_BASE__?: string; } }
const raw = window.__VITALS_BASE__ ?? '';
export const basePath = raw.startsWith('%') ? '/_vitals' : raw;
const apiBase = `${basePath}/api`;

export interface HealthEntry {
  name: string;
  status: 'Healthy' | 'Degraded' | 'Unhealthy';
  description?: string | null;
  durationMs: number;
}

export interface Vitals {
  uptimeSeconds: number;
  managedMemoryBytes: number;
  workingSetBytes: number;
  gen0: number;
  gen1: number;
  gen2: number;
  threadCount: number;
  processorCount: number;
  cpuPercent: number;
  totalAllocatedBytes: number;
  assemblyCount: number;
  serverGc: boolean;
  runtime: string;
  os: string;
  environment: string;
  overallHealth?: string | null;
  healthChecks: HealthEntry[];
}

export const api = {
  get: (signal?: AbortSignal) =>
    fetch(`${apiBase}/vitals`, { signal }).then(r => { if (!r.ok) throw new Error(`${r.status}`); return r.json() as Promise<Vitals>; }),
};
