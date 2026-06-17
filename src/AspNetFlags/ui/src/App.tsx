import { useEffect, useState } from 'react';
import { api, basePath, type Flag } from './api';
import { timeAgo, fmtDateTime } from './format';
import SuiteShell from './SuiteShell';

const POLL_MS = 4000;

export default function App() {
  const [flags, setFlags] = useState<Flag[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [tick, setTick] = useState(0);
  const [live, setLive] = useState(true);
  const [q, setQ] = useState('');
  const [adding, setAdding] = useState('');

  useEffect(() => {
    if (!live) return;
    const t = window.setInterval(() => setTick((n) => n + 1), POLL_MS);
    return () => window.clearInterval(t);
  }, [live]);

  useEffect(() => {
    const ctrl = new AbortController();
    api.all(ctrl.signal).then((f) => { setFlags(f); setError(null); })
      .catch((e) => { if (!ctrl.signal.aborted) setError(String(e.message ?? e)); });
    return () => ctrl.abort();
  }, [tick]);

  // optimistic flip, then persist
  const toggle = async (f: Flag) => {
    const next = !f.enabled;
    setFlags((cur) => cur?.map((x) => x.name === f.name ? { ...x, enabled: next } : x) ?? cur);
    await api.set(f.name, next);
    setTick((n) => n + 1);
  };

  const remove = async (f: Flag) => {
    setFlags((cur) => cur?.filter((x) => x.name !== f.name) ?? cur);
    await api.remove(f.name);
    setTick((n) => n + 1);
  };

  const add = async () => {
    const name = adding.trim();
    if (!name) return;
    setAdding('');
    await api.set(name, false);
    setTick((n) => n + 1);
  };

  const shown = flags?.filter((f) => !q || f.name.toLowerCase().includes(q.toLowerCase()));

  return (
    <SuiteShell current={basePath}>
    <div className="main" style={{ height: '100vh' }}>
      <div className="topbar">
        <h1>Feature Flags</h1>
        <span className="sub">{flags?.length ?? 0} flags</span>
        {flags && flags.length > 0 && (
          <input className="search" placeholder="Filter…" value={q} onChange={(e) => setQ(e.target.value)} style={{ marginLeft: 16, width: 200 }} />
        )}
        <button className={`live-toggle${live ? ' on' : ''}`} onClick={() => setLive((v) => !v)} style={{ marginLeft: 'auto' }}>
          <span className="live-dot" />{live ? 'live' : 'paused'}
        </button>
      </div>

      {error ? (
        <div className="state-box error"><div className="title">Couldn't load flags</div><div>{error}</div></div>
      ) : !flags ? (
        <div className="table-wrap"><div className="skeleton-rows">{[0, 1, 2].map((i) => <div key={i} className="skeleton" style={{ width: `${70 - i * 10}%` }} />)}</div></div>
      ) : flags.length === 0 ? (
        <Empty />
      ) : (
        <div className="page-scroll">
          <div className="mini-list" style={{ maxWidth: 720 }}>
            <div className="flag-add">
              <input
                placeholder="new flag name…"
                value={adding}
                onChange={(e) => setAdding(e.target.value)}
                onKeyDown={(e) => { if (e.key === 'Enter') add(); }}
              />
              <button className="btn" onClick={add} disabled={!adding.trim()}>Add</button>
            </div>
            {shown!.map((f) => (
              <div key={f.name} className="flag-row">
                <div className="info">
                  <div className="fname">{f.name}</div>
                  {f.description && <div className="fdesc">{f.description}</div>}
                </div>
                <span className="fmeta" title={fmtDateTime(f.updatedAt)}>{timeAgo(f.updatedAt)}</span>
                <label className={`switch${f.enabled ? ' on' : ''}`} title={f.enabled ? 'enabled' : 'disabled'}>
                  <input type="checkbox" checked={f.enabled} onChange={() => toggle(f)} aria-label={`${f.name} ${f.enabled ? 'enabled' : 'disabled'}`} />
                  <span className="track" />
                  <span className="thumb" />
                </label>
                <button className="frow-del" title="Delete flag" aria-label={`Delete ${f.name}`} onClick={() => remove(f)}>✕</button>
              </div>
            ))}
            {shown!.length === 0 && <div className="fdesc" style={{ padding: '14px 18px' }}>No flags match "{q}".</div>}
          </div>
        </div>
      )}
    </div>
    </SuiteShell>
  );
}

function Empty() {
  const snippet = `builder.Services.AddFlags();   // 1. register
app.UseFlags();                // 2. serve /_flags

// flags appear here the first time your code checks one:
if (flags.IsEnabled("new-checkout")) { ... }`;
  return (
    <div className="state-box">
      <div className="title">No flags yet</div>
      <div className="hint">Flags show up here as your code checks them. Then flip them from this page.</div>
      <pre className="snippet">{snippet}</pre>
    </div>
  );
}
