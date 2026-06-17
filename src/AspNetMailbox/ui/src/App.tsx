import { useEffect, useRef, useState } from 'react';
import { api, type MailSummary, type MailDetail } from './api';
import { fmtBytes, fmtTime, timeAgo } from './format';

const POLL_MS = 4000;

export default function App() {
  const [items, setItems] = useState<MailSummary[]>([]);
  const [total, setTotal] = useState(0);
  const [search, setSearch] = useState('');
  const [live, setLive] = useState(true);
  const [tick, setTick] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [openId, setOpenId] = useState<string | null>(null);
  const timer = useRef<number>();

  useEffect(() => {
    if (!live) return;
    const t = window.setInterval(() => setTick((n) => n + 1), POLL_MS);
    return () => window.clearInterval(t);
  }, [live]);

  useEffect(() => {
    const ctrl = new AbortController();
    api.list(search, 1, ctrl.signal)
      .then((r) => { setItems(r.items); setTotal(r.totalCount); setError(null); setLoading(false); })
      .catch((e) => { if (!ctrl.signal.aborted) { setError(String(e.message ?? e)); setLoading(false); } });
    return () => ctrl.abort();
  }, [search, tick]);

  const clearAll = async () => {
    if (!window.confirm('Delete all captured mail?')) return;
    await api.clear();
    setTick((n) => n + 1);
  };

  return (
    <div className="main" style={{ height: '100vh' }}>
      <div className="topbar">
        <h1>Mailbox</h1>
        <span className="sub">{total.toLocaleString()} captured</span>
        <input
          className="search"
          placeholder="Search subject or sender…"
          defaultValue={search}
          onChange={(e) => {
            window.clearTimeout(timer.current);
            const v = e.target.value;
            timer.current = window.setTimeout(() => setSearch(v), 300);
          }}
        />
        <button className={`live-toggle${live ? ' on' : ''}`} onClick={() => setLive((v) => !v)}>
          <span className="live-dot" />
          {live ? 'live' : 'paused'}
        </button>
        <button className="btn danger" onClick={clearAll}>Clear</button>
      </div>

      {error ? (
        <div className="state-box error"><div className="title">Couldn't load mail</div><div>{error}</div></div>
      ) : loading ? (
        <div className="table-wrap"><div className="skeleton-rows">{[0, 1, 2, 3, 4].map((i) => <div key={i} className="skeleton" style={{ width: `${85 - i * 8}%` }} />)}</div></div>
      ) : items.length === 0 ? (
        <Empty />
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>From</th>
                <th>Subject</th>
                <th className="fit">To</th>
                <th className="fit">When</th>
              </tr>
            </thead>
            <tbody>
              {items.map((m) => (
                <tr key={m.id} onClick={() => setOpenId(m.id)}>
                  <td className="fit" style={{ maxWidth: 220 }} title={m.from}>{m.from}</td>
                  <td className="primary">
                    {m.subject || <span className="dim">(no subject)</span>}
                    {m.attachments > 0 && <span className="tag other" style={{ marginLeft: 8 }}>{m.attachments} att</span>}
                  </td>
                  <td className="dim fit" title={m.to.join(', ')}>{m.to[0]}{m.to.length > 1 ? ` +${m.to.length - 1}` : ''}</td>
                  <td className="dim fit" title={fmtTime(m.receivedAt)}>{timeAgo(m.receivedAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {openId && <Detail id={openId} onClose={() => setOpenId(null)} />}
    </div>
  );
}

function Empty() {
  const snippet = `// point your SMTP sender at the sink in Development
builder.Services.AddMailbox();           // 1. register
app.UseMailbox();                        // 2. serve /_mailbox

// send to localhost:2525, e.g. with MailKit / SmtpClient`;
  return (
    <div className="state-box">
      <div className="title">No mail captured yet</div>
      <div className="hint">Send an email through the in-process sink and it shows up here.</div>
      <pre className="snippet">{snippet}</pre>
    </div>
  );
}

type Tab = 'preview' | 'html' | 'text' | 'headers' | 'raw' | 'attachments';

function Detail({ id, onClose }: { id: string; onClose: () => void }) {
  const [m, setM] = useState<(MailDetail) | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [tab, setTab] = useState<Tab>('preview');
  const [lightPreview, setLightPreview] = useState(true);

  useEffect(() => {
    setM(null); setErr(null); setTab('preview');
    api.get(id).then(setM).catch((e) => setErr(String(e.message ?? e)));
  }, [id]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose]);

  const tabs: { id: Tab; label: string; n?: number }[] = m ? [
    { id: 'preview', label: 'Preview' },
    { id: 'html', label: 'HTML' },
    { id: 'text', label: 'Text' },
    { id: 'headers', label: 'Headers', n: Object.keys(m.headers ?? {}).length },
    { id: 'raw', label: 'Raw' },
    { id: 'attachments', label: 'Attachments', n: m.attachments?.length ?? 0 },
  ] : [];

  return (
    <>
      <div className="overlay" onClick={onClose} />
      <aside className="detail">
        <div className="detail-head">
          <div className="row1">
            <span className="title">{m?.subject || (m ? '(no subject)' : '')}</span>
            {m && <a className="btn" href={api.emlUrl(m.id)} download style={{ marginLeft: 'auto' }}>Download .eml</a>}
            <button className="detail-close" onClick={onClose} aria-label="Close">✕</button>
          </div>
          {m && (
            <div className="meta">
              <span>from {m.from}</span>
              <span>to {m.to.join(', ')}</span>
              <span>{fmtTime(m.receivedAt)}</span>
            </div>
          )}
        </div>

        {err && <div className="state-box error"><div className="title">{err}</div></div>}
        {!m && !err && <div className="detail-body"><div className="skeleton-rows"><div className="skeleton" /><div className="skeleton" style={{ width: '60%' }} /></div></div>}

        {m && (
          <>
            <div className="dtabs">
              {tabs.map((t) => (
                <button key={t.id} className={`dtab${t.id === tab ? ' active' : ''}`} onClick={() => setTab(t.id)}>
                  {t.label}{t.n !== undefined && t.n > 0 && <span className="n">{t.n}</span>}
                </button>
              ))}
            </div>
            <div className="detail-body">
              {tab === 'preview' && (
                m.htmlBody ? (
                  <>
                    <div className="preview-bar">
                      <span className="dim">background</span>
                      <button className={`seg${lightPreview ? ' on' : ''}`} onClick={() => setLightPreview(true)}>light</button>
                      <button className={`seg${!lightPreview ? ' on' : ''}`} onClick={() => setLightPreview(false)}>dark</button>
                    </div>
                    {/* sandboxed: captured email is untrusted, never let it run scripts in the dashboard */}
                    <iframe title="preview" sandbox="" srcDoc={m.htmlBody}
                      style={{ width: '100%', height: '56vh', border: '1px solid var(--border)', borderRadius: 8, background: lightPreview ? '#fff' : '#15191f' }} />
                  </>
                ) : <pre className="code">{m.textBody || '(no body)'}</pre>
              )}
              {tab === 'html' && <pre className="code">{m.htmlBody || '(no HTML body)'}</pre>}
              {tab === 'text' && <pre className="code">{m.textBody || '(no text body)'}</pre>}
              {tab === 'headers' && (
                <pre className="code">{Object.entries(m.headers ?? {}).map(([k, v]) => `${k}: ${v}`).join('\n')}</pre>
              )}
              {tab === 'raw' && <pre className="code">{m.raw || '(raw source unavailable)'}</pre>}
              {tab === 'attachments' && (
                (m.attachments?.length ?? 0) === 0
                  ? <div className="state-box"><div>No attachments</div></div>
                  : m.attachments.map((a) => (
                      <div key={a.index} className="sub-row">
                        <a className="grow" href={api.attachmentUrl(m.id, a.index)} target="_blank" rel="noreferrer"
                           style={{ color: 'var(--accent)' }}>{a.fileName}</a>
                        <span className="dim">{a.contentType}</span>
                        <span>{fmtBytes(a.size)}</span>
                      </div>
                    ))
              )}
            </div>
          </>
        )}
      </aside>
    </>
  );
}
