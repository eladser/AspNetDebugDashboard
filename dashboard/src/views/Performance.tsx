import { useEffect, useRef, useState } from 'react';
import { api } from '../api';
import type { PerfMetrics } from '../types';
import { fmtDuration } from '../format';
import { ErrorState, Skeleton } from '../ui';

const STATUS_COLORS: Record<string, string> = {
  '2': 'var(--ok)', '3': 'var(--info)', '4': 'var(--warn)', '5': 'var(--err)',
};

export function Performance({ tick }: { tick: number }) {
  const [perf, setPerf] = useState<PerfMetrics | null>(null);
  const [error, setError] = useState<string | null>(null);
  const loaded = useRef(false);

  const load = async () => {
    try {
      setPerf(await api.performance());
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
  if (!perf) return <div className="page-scroll"><Skeleton rows={10} /></div>;

  const maxAvg = Math.max(...perf.slowestEndpoints.map((e) => e.averageTime), 1);

  const cards = [
    { label: 'Requests (1h)', value: perf.totalRequests.toLocaleString() },
    { label: 'Req / min', value: perf.requestsPerMinute.toFixed(1) },
    { label: 'Avg', value: fmtDuration(perf.averageResponseTime) },
    { label: 'Median', value: fmtDuration(perf.medianResponseTime) },
    { label: 'P95', value: fmtDuration(perf.p95ResponseTime), warn: perf.p95ResponseTime > 1000 },
    { label: 'P99', value: fmtDuration(perf.p99ResponseTime), warn: perf.p99ResponseTime > 2000 },
    { label: 'Error rate', value: `${perf.errorRate.toFixed(1)}%`, err: perf.errorRate > 5 },
  ];

  return (
    <div className="page-scroll">
      <div className="stat-row">
        {cards.map((c) => (
          <div key={c.label} className="stat">
            <div className={`value${c.err ? ' err' : c.warn ? ' warn' : ''}`}>{c.value}</div>
            <div className="label">{c.label}</div>
          </div>
        ))}
      </div>

      <div className="two-col">
        <div>
          <h2 className="section-title">Slowest endpoints (avg, last hour)</h2>
          {perf.slowestEndpoints.length === 0 ? (
            <div className="dist-legend">No traffic in the last hour</div>
          ) : (
            <div className="mini-list">
              {perf.slowestEndpoints.map((e) => (
                <div key={e.endpoint} className="mini-row static">
                  <span className="grow" title={e.endpoint}>{e.endpoint}</span>
                  <span className="bar-cell" style={{ width: 130 }}>
                    <span className="track"><i style={{ width: `${(e.averageTime / maxAvg) * 100}%` }} /></span>
                  </span>
                  <span className="ms">{fmtDuration(e.averageTime)}</span>
                  <span className="n">×{e.requestCount}</span>
                </div>
              ))}
            </div>
          )}
        </div>
        <div>
          <h2 className="section-title">Status codes (last hour)</h2>
          {perf.statusCodeDistribution.length === 0 ? (
            <div className="dist-legend">No traffic in the last hour</div>
          ) : (
            <div className="mini-list">
              {perf.statusCodeDistribution.map((s) => (
                <div key={s.statusCode} className="mini-row static">
                  <span className="status" style={{ color: STATUS_COLORS[String(s.statusCode)[0]] ?? 'var(--muted)' }}>
                    {s.statusCode}
                  </span>
                  <span className="grow" />
                  <span className="n">{s.count}</span>
                  <span style={{ width: 52, textAlign: 'right' }}>{s.percentage.toFixed(1)}%</span>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      <div className="dist-legend">Window: last 60 minutes, recomputed on refresh</div>
    </div>
  );
}
