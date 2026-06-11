import { useEffect, useRef, useState } from 'react';
import { api } from '../api';
import type { DebugStats } from '../types';
import { fmtDuration, timeAgo } from '../format';
import { ErrorState, MethodTag, Skeleton } from '../ui';

const STATUS_COLORS: Record<string, string> = {
  '2': 'var(--ok)', '3': 'var(--info)', '4': 'var(--warn)', '5': 'var(--err)',
};

function StatusDistribution({ dist }: { dist: Record<string, number> }) {
  const entries = Object.entries(dist).sort(([a], [b]) => Number(a) - Number(b));
  const total = entries.reduce((s, [, n]) => s + n, 0);
  if (total === 0) return <div className="dist-legend">No requests yet</div>;
  return (
    <>
      <div className="dist-bar">
        {entries.map(([code, n]) => (
          <div
            key={code}
            style={{ width: `${(n / total) * 100}%`, background: STATUS_COLORS[code[0]] ?? 'var(--faint)' }}
            title={`${code}: ${n}`}
          />
        ))}
      </div>
      <div className="dist-legend">
        {entries.map(([code, n]) => (
          <span key={code}>
            <span className="swatch" style={{ background: STATUS_COLORS[code[0]] ?? 'var(--faint)' }} />
            {code} · {n}
          </span>
        ))}
      </div>
    </>
  );
}

export function Overview({
  tick,
  onOpen,
}: {
  tick: number;
  onOpen: (kind: 'request' | 'query', id: string) => void;
}) {
  const [stats, setStats] = useState<DebugStats | null>(null);
  const [error, setError] = useState<string | null>(null);
  const loaded = useRef(false);

  const load = async () => {
    try {
      setStats(await api.stats());
      setError(null);
      loaded.current = true;
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  };

  useEffect(() => {
    load();
  }, [tick]); // eslint-disable-line react-hooks/exhaustive-deps

  if (error && !loaded.current) return <ErrorState message={error} onRetry={load} />;
  if (!stats) return <div className="page-scroll"><Skeleton rows={10} /></div>;

  const totalReqs = Object.values(stats.statusCodeDistribution).reduce((s, n) => s + n, 0);
  const failed = Object.entries(stats.statusCodeDistribution)
    .filter(([c]) => Number(c) >= 400)
    .reduce((s, [, n]) => s + n, 0);
  const errorRate = totalReqs > 0 ? (failed / totalReqs) * 100 : 0;

  const counters = [
    { label: 'Requests', value: stats.totalRequests.toLocaleString() },
    { label: 'SQL queries', value: stats.totalSqlQueries.toLocaleString() },
    { label: 'Logs', value: stats.totalLogs.toLocaleString() },
    { label: 'Exceptions', value: stats.totalExceptions.toLocaleString(), err: stats.totalExceptions > 0 },
    { label: 'Error rate', value: `${errorRate.toFixed(1)}%`, err: errorRate > 5 },
    { label: 'Avg response', value: fmtDuration(stats.averageResponseTime) },
    { label: 'Avg query', value: fmtDuration(stats.averageSqlTime) },
  ];

  const exTypes = Object.entries(stats.exceptionTypeDistribution).sort(([, a], [, b]) => b - a).slice(0, 8);

  return (
    <div className="page-scroll">
      <div className="stat-row">
        {counters.map((c) => (
          <div key={c.label} className="stat">
            <div className={`value${c.err ? ' err' : ''}`}>{c.value}</div>
            <div className="label">{c.label}</div>
          </div>
        ))}
      </div>

      <div className="two-col">
        <div>
          <h2 className="section-title">Status codes</h2>
          <StatusDistribution dist={stats.statusCodeDistribution} />
        </div>
        <div>
          <h2 className="section-title">Methods</h2>
          {Object.keys(stats.requestMethodDistribution).length === 0 ? (
            <div className="dist-legend">No requests yet</div>
          ) : (
            <div className="mini-list">
              {Object.entries(stats.requestMethodDistribution)
                .sort(([, a], [, b]) => b - a)
                .map(([method, n]) => (
                  <div key={method} className="mini-row static">
                    <MethodTag method={method} />
                    <span className="grow" />
                    <span className="n">{n}</span>
                  </div>
                ))}
            </div>
          )}
        </div>
      </div>

      <div className="two-col">
        <div>
          <h2 className="section-title">Slowest requests</h2>
          {stats.slowestRequests.length === 0 ? (
            <div className="dist-legend">Nothing recorded yet</div>
          ) : (
            <div className="mini-list">
              {stats.slowestRequests.map((r) => (
                <div key={r.id} className="mini-row" onClick={() => onOpen('request', r.id)}>
                  <MethodTag method={r.method} />
                  <span className="grow">{r.path}</span>
                  <span className="ms">{fmtDuration(r.executionTimeMs)}</span>
                </div>
              ))}
            </div>
          )}
        </div>
        <div>
          <h2 className="section-title">Slowest queries</h2>
          {stats.slowestQueries.length === 0 ? (
            <div className="dist-legend">Nothing recorded yet</div>
          ) : (
            <div className="mini-list">
              {stats.slowestQueries.map((q) => (
                <div key={q.id} className="mini-row" onClick={() => onOpen('query', q.id)}>
                  <span className="grow">{q.query}</span>
                  <span className="ms">{fmtDuration(q.executionTimeMs)}</span>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {exTypes.length > 0 && (
        <div className="two-col">
          <div>
            <h2 className="section-title">Exception types</h2>
            <div className="mini-list">
              {exTypes.map(([type, n]) => (
                <div key={type} className="mini-row static">
                  <span className="grow" style={{ color: 'var(--err)' }}>{type.split('.').pop()}</span>
                  <span className="n">×{n}</span>
                </div>
              ))}
            </div>
          </div>
          <div />
        </div>
      )}

      <div className="dist-legend">Updated {timeAgo(stats.lastUpdated)}</div>
    </div>
  );
}
