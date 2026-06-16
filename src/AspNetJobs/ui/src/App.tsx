import { useEffect, useState } from 'react';
import { api, type Job } from './api';
import { fmtDuration, timeAgo, fmtDateTime } from './format';

const POLL_MS = 2000;

export default function App() {
  const [jobs, setJobs] = useState<Job[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [open, setOpen] = useState<string | null>(null);
  const [tick, setTick] = useState(0);
  const [live, setLive] = useState(true);

  useEffect(() => {
    if (!live) return;
    const t = window.setInterval(() => setTick(n => n + 1), POLL_MS);
    return () => window.clearInterval(t);
  }, [live]);

  useEffect(() => {
    const ctrl = new AbortController();
    api.list(ctrl.signal).then(j => { setJobs(j); setError(null); })
      .catch(e => { if (!ctrl.signal.aborted) setError(String(e.message ?? e)); });
    return () => ctrl.abort();
  }, [tick]);

  const running = jobs?.filter(j => j.status === 'Running').length ?? 0;
  const failed = jobs?.filter(j => j.status === 'Failed').length ?? 0;

  const clear = async () => { await api.clear(); setTick(n => n + 1); };

  return (
    <div className="main" style={{ height: '100vh' }}>
      <div className="topbar">
        <h1>Background Jobs</h1>
        <span className="sub">{jobs?.length ?? 0} jobs{running ? ` · ${running} running` : ''}{failed ? ` · ${failed} failed` : ''}</span>
        <div style={{ marginLeft: 'auto', display: 'flex', gap: 10 }}>
          <button className="live-toggle" onClick={clear} title="Remove finished jobs">clear</button>
          <button className={`live-toggle${live ? ' on' : ''}`} onClick={() => setLive(v => !v)}>
            <span className="live-dot" />{live ? 'live' : 'paused'}
          </button>
        </div>
      </div>

      {error ? (
        <div className="state-box error"><div className="title">Couldn't load jobs</div><div>{error}</div></div>
      ) : !jobs ? (
        <div className="table-wrap"><div className="skeleton-rows">{[0, 1, 2, 3].map(i => <div key={i} className="skeleton" style={{ width: `${75 - i * 9}%` }} />)}</div></div>
      ) : jobs.length === 0 ? (
        <Empty />
      ) : (
        <div className="page-scroll">
          <div className="table-wrap">
            <table>
              <thead>
                <tr><th style={{ width: 110 }}>Status</th><th>Job</th><th style={{ width: 130 }}>Enqueued</th><th style={{ width: 110 }}>Duration</th></tr>
              </thead>
              <tbody>
                {jobs.map(j => {
                  const expandable = !!j.error;
                  return [
                    <tr key={j.jobId} onClick={() => expandable && setOpen(open === j.jobId ? null : j.jobId)} style={{ cursor: expandable ? 'pointer' : 'default' }}>
                      <td><Badge status={j.status} /></td>
                      <td style={{ fontFamily: 'var(--mono)', fontSize: 13 }}>{j.name}</td>
                      <td style={{ color: 'var(--faint)', fontFamily: 'var(--mono)', fontSize: 12 }} title={fmtDateTime(j.enqueuedAt)}>{timeAgo(j.enqueuedAt)}</td>
                      <td style={{ fontFamily: 'var(--mono)', fontSize: 12, color: 'var(--muted)' }}>{j.durationMs != null ? fmtDuration(j.durationMs) : '—'}</td>
                    </tr>,
                    open === j.jobId && j.error ? (
                      <tr key={j.jobId + '-err'} className="err-row"><td colSpan={4}><pre className="err-trace">{j.error}</pre></td></tr>
                    ) : null,
                  ];
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}

function Badge({ status }: { status: Job['status'] }) {
  return <span className={`jbadge ${status.toLowerCase()}`}>{status === 'Running' && <span className="run-dot" />}{status}</span>;
}

function Empty() {
  const snippet = `builder.Services.AddJobs();   // 1. register + start the runner
app.UseJobs();                // 2. serve /_jobs

// enqueue from anywhere:
public class Reports(IJobQueue jobs)
{
    public void Nightly() =>
        jobs.Enqueue("nightly-report", async ct => { await Build(ct); });
}`;
  return (
    <div className="state-box">
      <div className="title">No jobs yet</div>
      <div className="hint">Enqueue work through <code>IJobQueue</code> and it shows up here as it runs.</div>
      <pre className="snippet">{snippet}</pre>
    </div>
  );
}
