import { createRef, useEffect, useRef, useState } from 'react';
import { api } from './api';
import type { DebugStats, SearchHit } from './types';
import { Overview } from './views/Overview';
import { Performance } from './views/Performance';
import { Requests } from './views/Requests';
import { Queries } from './views/Queries';
import { Logs } from './views/Logs';
import { Exceptions } from './views/Exceptions';
import { DetailPanel, type DetailRef } from './views/Detail';
import { LevelTag, MethodTag, StatusCode } from './ui';

type Tab = 'overview' | 'performance' | 'requests' | 'queries' | 'logs' | 'exceptions';

const I = {
  pulse: <svg width="15" height="15" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"><path d="M1.5 8h3l2-4.5L10 12l2-4h2.5" /></svg>,
  gauge: <svg width="15" height="15" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"><path d="M2.5 12.5a6.5 6.5 0 1 1 11 0" /><path d="M8 9.5 11 6" /></svg>,
  swap: <svg width="15" height="15" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"><path d="M11 2.5 14 5.5l-3 3" /><path d="M14 5.5H5" /><path d="M5 13.5 2 10.5l3-3" /><path d="M2 10.5h9" /></svg>,
  db: <svg width="15" height="15" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5"><ellipse cx="8" cy="3.5" rx="5.5" ry="2" /><path d="M2.5 3.5v9c0 1.1 2.46 2 5.5 2s5.5-.9 5.5-2v-9" /><path d="M2.5 8c0 1.1 2.46 2 5.5 2s5.5-.9 5.5-2" /></svg>,
  lines: <svg width="15" height="15" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"><path d="M2.5 4h11" /><path d="M2.5 8h7" /><path d="M2.5 12h9" /></svg>,
  alert: <svg width="15" height="15" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"><path d="M8 2 1.8 13h12.4L8 2z" /><path d="M8 6.5v3" /><circle cx="8" cy="11.5" r="0.3" fill="currentColor" /></svg>,
  search: <svg width="13" height="13" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"><circle cx="7" cy="7" r="4.5" /><path d="m13.5 13.5-3.2-3.2" /></svg>,
  logo: <svg width="16" height="16" viewBox="0 0 16 16" fill="none"><rect x="1" y="1" width="14" height="14" rx="3.5" stroke="var(--accent)" strokeWidth="1.5" /><path d="M4.5 8h2l1.5-3.5L10 11l1.5-3h0" stroke="var(--accent)" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" /></svg>,
};

const NAV: { group: string; tabs: { id: Tab; label: string; icon: React.ReactNode }[] }[] = [
  {
    group: 'Insight',
    tabs: [
      { id: 'overview', label: 'Overview', icon: I.pulse },
      { id: 'performance', label: 'Performance', icon: I.gauge },
    ],
  },
  {
    group: 'Traffic',
    tabs: [
      { id: 'requests', label: 'Requests', icon: I.swap },
      { id: 'queries', label: 'Queries', icon: I.db },
    ],
  },
  {
    group: 'Diagnostics',
    tabs: [
      { id: 'logs', label: 'Logs', icon: I.lines },
      { id: 'exceptions', label: 'Exceptions', icon: I.alert },
    ],
  },
];

const ALL_TABS = NAV.flatMap((g) => g.tabs);
const POLL_MS = 5000;

const tabFromHash = (): Tab => {
  const h = window.location.hash.replace('#', '');
  return ALL_TABS.some((t) => t.id === h) ? (h as Tab) : 'overview';
};

function hitId(h: SearchHit): string {
  return String(h.data.id ?? '');
}

function hitKind(h: SearchHit): DetailRef['kind'] {
  return h.type;
}

function GlobalSearch({ onOpen }: { onOpen: (ref: DetailRef) => void }) {
  const [term, setTerm] = useState('');
  const [hits, setHits] = useState<SearchHit[] | null>(null);
  const [open, setOpen] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  const timer = useRef<number>();

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        inputRef.current?.focus();
        inputRef.current?.select();
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  useEffect(() => {
    window.clearTimeout(timer.current);
    if (term.trim().length < 2) {
      setHits(null);
      return;
    }
    timer.current = window.setTimeout(async () => {
      try {
        setHits(await api.search(term.trim()));
      } catch {
        setHits([]);
      }
    }, 250);
  }, [term]);

  const groups = new Map<string, SearchHit[]>();
  for (const h of hits ?? []) {
    const k = h.type;
    if (!groups.has(k)) groups.set(k, []);
    groups.get(k)!.push(h);
  }

  const labels: Record<string, string> = { request: 'Requests', query: 'Queries', log: 'Logs', exception: 'Exceptions' };

  return (
    <div className="gsearch">
      <span className="icon">{I.search}</span>
      <input
        ref={inputRef}
        value={term}
        placeholder="Search everything…"
        onChange={(e) => setTerm(e.target.value)}
        onFocus={() => setOpen(true)}
        onBlur={() => setTimeout(() => setOpen(false), 150)}
        onKeyDown={(e) => {
          if (e.key === 'Escape') { setTerm(''); (e.target as HTMLInputElement).blur(); }
          e.stopPropagation();
        }}
      />
      <span className="kbd-hint"><kbd>ctrl K</kbd></span>
      {open && hits !== null && (
        <div className="gsearch-results">
          {hits.length === 0 && <div className="gsearch-empty">No matches for "{term}"</div>}
          {[...groups.entries()].map(([type, items]) => (
            <div key={type}>
              <div className="gsearch-group">{labels[type] ?? type}</div>
              {items.map((h) => {
                const d = h.data as Record<string, unknown>;
                return (
                  <button
                    key={hitId(h)}
                    className="gsearch-row"
                    onMouseDown={(e) => e.preventDefault()}
                    onClick={() => {
                      onOpen({ kind: hitKind(h), id: hitId(h) });
                      setTerm('');
                      setOpen(false);
                    }}
                  >
                    {type === 'request' && (
                      <>
                        <MethodTag method={String(d.method ?? '')} />
                        <span className="grow">{String(d.path ?? '')}</span>
                        <StatusCode code={Number(d.statusCode ?? 0)} />
                      </>
                    )}
                    {type === 'query' && <span className="grow">{String(d.query ?? '')}</span>}
                    {type === 'log' && (
                      <>
                        <LevelTag level={String(d.level ?? 'Info')} />
                        <span className="grow">{String(d.message ?? '')}</span>
                      </>
                    )}
                    {type === 'exception' && (
                      <span className="grow" style={{ color: 'var(--err)' }}>{String(d.message ?? '')}</span>
                    )}
                  </button>
                );
              })}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

export default function App() {
  const [tab, setTabState] = useState<Tab>(tabFromHash);
  const [live, setLive] = useState(true);
  const [tick, setTick] = useState(0);
  const [detail, setDetail] = useState<DetailRef | null>(null);
  const [counts, setCounts] = useState<DebugStats | null>(null);
  const searchRef = createRef<HTMLInputElement>();

  const setTab = (t: Tab) => {
    setTabState(t);
    window.location.hash = t === 'overview' ? '' : t;
  };

  useEffect(() => {
    const onHash = () => setTabState(tabFromHash());
    window.addEventListener('hashchange', onHash);
    return () => window.removeEventListener('hashchange', onHash);
  }, []);

  useEffect(() => {
    if (!live) return;
    const t = window.setInterval(() => setTick((n) => n + 1), POLL_MS);
    return () => window.clearInterval(t);
  }, [live]);

  // sidebar counts piggyback on the same tick
  useEffect(() => {
    api.stats().then(setCounts).catch(() => {});
  }, [tick]);

  // `/` focuses the current view's filter box
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const tag = (e.target as HTMLElement).tagName;
      if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') return;
      if (e.key === '/') {
        e.preventDefault();
        searchRef.current?.focus();
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [searchRef]);

  const navCount = (id: Tab): { n: number; alert?: boolean } | null => {
    if (!counts) return null;
    switch (id) {
      case 'requests': return { n: counts.totalRequests };
      case 'queries': return { n: counts.totalSqlQueries };
      case 'logs': return { n: counts.totalLogs };
      case 'exceptions': return { n: counts.totalExceptions, alert: counts.totalExceptions > 0 };
      default: return null;
    }
  };

  const clearAll = async () => {
    if (!window.confirm('Delete all captured data?')) return;
    await api.clearAll();
    setTick((n) => n + 1);
  };

  const viewProps = { tick, navEnabled: !detail, searchRef, onOpen: setDetail };
  const activeLabel = ALL_TABS.find((t) => t.id === tab)?.label ?? '';

  return (
    <div className="shell">
      <nav className="sidebar">
        <div className="brand">{I.logo} debug</div>
        {NAV.map((g) => (
          <div key={g.group}>
            <div className="nav-group">{g.group}</div>
            {g.tabs.map((t) => {
              const c = navCount(t.id);
              return (
                <button
                  key={t.id}
                  className={`nav-item${tab === t.id ? ' active' : ''}`}
                  onClick={() => setTab(t.id)}
                >
                  {t.icon}
                  {t.label}
                  {c && c.n > 0 && <span className={`count${c.alert ? ' alert' : ''}`}>{c.n.toLocaleString()}</span>}
                </button>
              );
            })}
          </div>
        ))}
        <div className="sidebar-footer">
          <a href={api.exportUrl}>export json</a>
          <span>AspNetDebugDashboard</span>
        </div>
      </nav>

      <main className="main">
        <div className="topbar">
          <h1>{activeLabel}</h1>
          <span className="sub" />
          <GlobalSearch onOpen={setDetail} />
          <button className={`live-toggle${live ? ' on' : ''}`} onClick={() => setLive((v) => !v)}>
            <span className="live-dot" />
            {live ? 'live' : 'paused'}
          </button>
          <button className="btn danger" onClick={clearAll}>Clear</button>
        </div>

        {tab === 'overview' && <Overview tick={tick} onOpen={(kind, id) => setDetail({ kind, id })} />}
        {tab === 'performance' && <Performance tick={tick} />}
        {tab === 'requests' && <Requests {...viewProps} />}
        {tab === 'queries' && <Queries {...viewProps} />}
        {tab === 'logs' && <Logs {...viewProps} />}
        {tab === 'exceptions' && <Exceptions {...viewProps} />}
      </main>

      {detail && <DetailPanel refs={detail} onClose={() => setDetail(null)} onOpen={setDetail} />}
    </div>
  );
}
