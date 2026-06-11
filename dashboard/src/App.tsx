import { useEffect, useState } from 'react';
import { api } from './api';
import type { DebugStats } from './types';
import { Overview } from './views/Overview';
import { Requests } from './views/Requests';
import { Queries } from './views/Queries';
import { Logs } from './views/Logs';
import { Exceptions } from './views/Exceptions';
import { DetailPanel, type DetailRef } from './views/Detail';

type Tab = 'overview' | 'requests' | 'queries' | 'logs' | 'exceptions';

const TABS: { id: Tab; label: string }[] = [
  { id: 'overview', label: 'Overview' },
  { id: 'requests', label: 'Requests' },
  { id: 'queries', label: 'Queries' },
  { id: 'logs', label: 'Logs' },
  { id: 'exceptions', label: 'Exceptions' },
];

const POLL_MS = 5000;

const tabFromHash = (): Tab => {
  const h = window.location.hash.replace('#', '');
  return TABS.some((t) => t.id === h) ? (h as Tab) : 'overview';
};

export default function App() {
  const [tab, setTabState] = useState<Tab>(tabFromHash);

  const setTab = (t: Tab) => {
    setTabState(t);
    window.location.hash = t === 'overview' ? '' : t;
  };

  useEffect(() => {
    const onHash = () => setTabState(tabFromHash());
    window.addEventListener('hashchange', onHash);
    return () => window.removeEventListener('hashchange', onHash);
  }, []);
  const [live, setLive] = useState(true);
  const [tick, setTick] = useState(0);
  const [detail, setDetail] = useState<DetailRef | null>(null);
  const [counts, setCounts] = useState<DebugStats | null>(null);

  useEffect(() => {
    if (!live) return;
    const t = window.setInterval(() => setTick((n) => n + 1), POLL_MS);
    return () => window.clearInterval(t);
  }, [live]);

  // sidebar counts piggyback on the same tick
  useEffect(() => {
    api.stats().then(setCounts).catch(() => {});
  }, [tick]);

  const navCount = (id: Tab): number | null => {
    if (!counts) return null;
    switch (id) {
      case 'requests': return counts.totalRequests;
      case 'queries': return counts.totalSqlQueries;
      case 'logs': return counts.totalLogs;
      case 'exceptions': return counts.totalExceptions;
      default: return null;
    }
  };

  const clearAll = async () => {
    if (!window.confirm('Delete all captured data?')) return;
    await api.clearAll();
    setTick((n) => n + 1);
  };

  return (
    <div className="shell">
      <nav className="sidebar">
        <div className="brand">
          <span className="brand-dot" />
          debug
        </div>
        {TABS.map((t) => {
          const n = navCount(t.id);
          return (
            <button
              key={t.id}
              className={`nav-item${tab === t.id ? ' active' : ''}`}
              onClick={() => setTab(t.id)}
            >
              {t.label}
              {n !== null && n > 0 && <span className="count">{n.toLocaleString()}</span>}
            </button>
          );
        })}
        <div className="sidebar-footer">
          <a href={api.exportUrl}>export json</a>
          <span>AspNetDebugDashboard</span>
        </div>
      </nav>

      <main className="main">
        <div className="topbar">
          <h1>{TABS.find((t) => t.id === tab)?.label}</h1>
          <button className={`live-toggle${live ? ' on' : ''}`} onClick={() => setLive((v) => !v)}>
            <span className="live-dot" />
            {live ? 'live' : 'paused'}
          </button>
          <button className="btn danger" onClick={clearAll}>
            Clear
          </button>
        </div>

        {tab === 'overview' && (
          <Overview tick={tick} onOpen={(kind, id) => setDetail({ kind, id })} />
        )}
        {tab === 'requests' && <Requests tick={tick} onOpen={setDetail} />}
        {tab === 'queries' && <Queries tick={tick} onOpen={setDetail} />}
        {tab === 'logs' && <Logs tick={tick} onOpen={setDetail} />}
        {tab === 'exceptions' && <Exceptions tick={tick} onOpen={setDetail} />}
      </main>

      {detail && <DetailPanel refs={detail} onClose={() => setDetail(null)} onOpen={setDetail} />}
    </div>
  );
}
