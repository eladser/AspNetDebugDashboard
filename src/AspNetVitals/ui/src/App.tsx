import { useEffect, useState } from 'react';
import { api, type Vitals } from './api';
import { fmtBytes } from './format';

const POLL_MS = 2000;

function uptime(s: number): string {
  const d = Math.floor(s / 86400), h = Math.floor((s % 86400) / 3600), m = Math.floor((s % 3600) / 60);
  if (d) return `${d}d ${h}h`;
  if (h) return `${h}h ${m}m`;
  if (m) return `${m}m ${Math.floor(s % 60)}s`;
  return `${Math.floor(s)}s`;
}

export default function App() {
  const [v, setV] = useState<Vitals | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [tick, setTick] = useState(0);
  const [live, setLive] = useState(true);

  useEffect(() => {
    if (!live) return;
    const t = window.setInterval(() => setTick(n => n + 1), POLL_MS);
    return () => window.clearInterval(t);
  }, [live]);

  useEffect(() => {
    const ctrl = new AbortController();
    api.get(ctrl.signal).then(d => { setV(d); setError(null); })
      .catch(e => { if (!ctrl.signal.aborted) setError(String(e.message ?? e)); });
    return () => ctrl.abort();
  }, [tick]);

  return (
    <div className="main" style={{ height: '100vh' }}>
      <div className="topbar">
        <h1>Vitals</h1>
        <span className="sub">{v ? `${v.runtime} · ${v.environment}` : ''}</span>
        <button className={`live-toggle${live ? ' on' : ''}`} onClick={() => setLive(x => !x)} style={{ marginLeft: 'auto' }}>
          <span className="live-dot" />{live ? 'live' : 'paused'}
        </button>
      </div>

      {error ? (
        <div className="state-box error"><div className="title">Couldn't load vitals</div><div>{error}</div></div>
      ) : !v ? (
        <div className="table-wrap"><div className="skeleton-rows">{[0, 1, 2].map(i => <div key={i} className="skeleton" style={{ width: `${60 - i * 8}%` }} />)}</div></div>
      ) : (
        <div className="page-scroll" style={{ padding: '20px 24px' }}>
          <div className="stat-grid">
            <Stat label="memory (managed)" value={fmtBytes(v.managedMemoryBytes)} />
            <Stat label="working set" value={fmtBytes(v.workingSetBytes)} />
            <Stat label="uptime" value={uptime(v.uptimeSeconds)} />
            <Stat label="threads" value={String(v.threadCount)} />
            <Stat label="gc (gen 0 / 1 / 2)" value={`${v.gen0} / ${v.gen1} / ${v.gen2}`} />
            <Stat label="processors" value={String(v.processorCount)} />
          </div>

          <div className="vh-head">
            <span className="vh-title">Health checks</span>
            {v.overallHealth && <span className={`hbadge ${v.overallHealth.toLowerCase()}`}>{v.overallHealth}</span>}
          </div>
          {v.overallHealth == null ? (
            <div className="vh-empty">No health checks registered. Add some with <code>AddHealthChecks()</code> and they show up here.</div>
          ) : v.healthChecks.length === 0 ? (
            <div className="vh-empty">Health reporting is on, but no individual checks are registered.</div>
          ) : (
            <div className="vh-list">
              {v.healthChecks.map(h => (
                <div key={h.name} className="vh-row">
                  <span className={`hdot ${h.status.toLowerCase()}`} />
                  <span className="vh-name">{h.name}</span>
                  {h.description && <span className="vh-desc">{h.description}</span>}
                  <span className="vh-dur">{Math.round(h.durationMs)} ms</span>
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="stat">
      <div className="stat-label">{label}</div>
      <div className="stat-value">{value}</div>
    </div>
  );
}
