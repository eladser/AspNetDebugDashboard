import { useEffect, useState } from 'react';
import { api, type Job, type JobStatus } from './api';
import { fmtDuration, timeAgo, fmtDateTime } from './format';

const POLL_MS = 2000;
const STATUSES: JobStatus[] = ['Pending', 'Running', 'Succeeded', 'Failed'];

export default function App() {
  const [jobs, setJobs] = useState<Job[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [open, setOpen] = useState<string | null>(null);
  const [tick, setTick] = useState(0);
  const [live, setLive] = useState(true);
  const [filter, setFilter] = useState<JobStatus | 'all'>('all');
  const [q, setQ] = useState('');

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

  const counts = (s: JobStatus) => jobs?.filter(j => j.status === s).length ?? 0;
  const shown = jobs?.filter(j =>
    (filter === 'all' || j.status === filter) &&
    (!q || j.name.toLowerCase().includes(q.toLowerCase())));

  const clear = async () => { await api.clear(); setTick(n => n + 1); };

  return (
    <div className="main" style={{ height: '100vh' }}>
      <div className="topbar">
        <h1>Background Jobs</h1>
        <span className="sub">{jobs?.length ?? 0} jobs</span>
        {jobs && jobs.length > 0 && (
          <input className="search" placeholder="Search jobs…" value={q} onChange={e => setQ(e.target.value)} style={{ marginLeft: 16, width: 200 }} />
        )}
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
          <div className="job-stats">
            <button className={`chip${filter === 'all' ? ' on' : ''}`} onClick={() => setFilter('all')}>
              <span className="cn">{jobs.length}</span> all
            </button>
            {STATUSES.map(s => (
              <button key={s} className={`chip ${s.toLowerCase()}${filter === s ? ' on' : ''}`} onClick={() => setFilter(filter === s ? 'all' : s)}>
                <span className="cn">{counts(s)}</span> {s.toLowerCase()}
              </button>
            ))}
          </div>
          <div className="table-wrap">
            <table>
              <thead>
                <tr><th style={{ width: 110 }}>Status</th><th>Job</th><th style={{ width: 130 }}>Enqueued</th><th style={{ width: 110 }}>Duration</th></tr>
              </thead>
              <tbody>
                {shown!.map(j => {
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
            {shown!.length === 0 && <div style={{ padding: '16px', color: 'var(--faint)', fontSize: 13 }}>No jobs match.</div>}
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
