import { useEffect, useMemo, useRef, useState } from 'react';
import { api } from '../api';
import type { RequestEntry } from '../types';
import { fmtDuration } from '../format';
import { StatusCode } from '../ui';
import { buildDraft, compareRows, countRedacted, parseHeaderLines, replayUrl, sendsBody, toFetchInit } from '../replay';
import type { DetailRef } from './Detail';

const METHODS = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'HEAD', 'OPTIONS'];
const POLL_MS = 300;
const POLL_FOR_MS = 5000;

// status is what fetch saw: 0 for a redirect, since redirect: 'manual' only gets an opaque response
type Result =
  | { state: 'sending' }
  | { state: 'waiting' }
  | { state: 'found'; replay: RequestEntry }
  | { state: 'missing'; status: number; error?: string };

const stop = (e: React.KeyboardEvent<HTMLElement>) => {
  if (e.key === 'Escape') (e.target as HTMLElement).blur();
  e.stopPropagation();
};

const wait = (ms: number) => new Promise((r) => setTimeout(r, ms));

export function Replay({ entry, onOpen }: { entry: RequestEntry; onOpen: (ref: DetailRef) => void }) {
  const draft = useMemo(() => buildDraft(entry), [entry]);
  const [method, setMethod] = useState(draft.method);
  const [path, setPath] = useState(draft.path);
  const [headerText, setHeaderText] = useState(draft.headerText);
  const [body, setBody] = useState(draft.body);
  const [result, setResult] = useState<Result | null>(null);
  const run = useRef(0);

  useEffect(() => () => { run.current++; }, []);

  const parsed = useMemo(() => parseHeaderLines(headerText), [headerText]);
  const redacted = countRedacted(path, headerText, body);
  const noBody = !sendsBody(method) || !!draft.bodyDisabledReason;
  const busy = result?.state === 'sending' || result?.state === 'waiting';
  const methods = METHODS.includes(method) ? METHODS : [method, ...METHODS];
  const target = replayUrl(path);

  const send = async () => {
    // this exact string is what fetch gets, so checking it here covers the send
    const url = replayUrl(path);
    if (!url || new URL(url).origin !== window.location.origin) return;

    const id = ++run.current;
    const live = () => id === run.current;
    setResult({ state: 'sending' });

    // anything newer than this when polling is the replay we just sent
    let before: string | undefined;
    try {
      before = (await api.requests({ page: 1, pageSize: 1, replayOf: entry.id })).items[0]?.id;
    } catch (e) {
      console.warn('replay: could not list earlier replays', e);
    }

    let status = 0;
    let error: string | undefined;
    try {
      const res = await fetch(url, toFetchInit({ ...draft, method, path, headerText, body }, entry.id));
      status = res.status;
    } catch (e) {
      error = e instanceof Error ? e.message : String(e);
    }
    if (!live()) return;
    setResult({ state: 'waiting' });

    const until = Date.now() + POLL_FOR_MS;
    while (live()) {
      try {
        const newest = (await api.requests({ page: 1, pageSize: 1, replayOf: entry.id })).items[0];
        if (newest && newest.id !== before) {
          if (live()) setResult({ state: 'found', replay: newest });
          return;
        }
      } catch (e) {
        error ??= e instanceof Error ? e.message : String(e);
      }
      if (Date.now() >= until) break;
      await wait(POLL_MS);
    }
    if (live()) setResult({ state: 'missing', status, error });
  };

  return (
    <div className="detail-body">
      <div className="replay-bar">
        <select className="select" value={method} onChange={(e) => setMethod(e.target.value)} onKeyDown={stop}>
          {methods.map((m) => <option key={m}>{m}</option>)}
        </select>
        <input
          className="search replay-path"
          value={path}
          spellCheck={false}
          onChange={(e) => setPath(e.target.value)}
          onKeyDown={stop}
        />
        <button className="btn replay-send" onClick={send} disabled={busy || !target || parsed.errors.length > 0}>
          Send {method}
        </button>
      </div>

      {result && <ReplayResult result={result} original={entry} onOpen={onOpen} />}

      <ul className="replay-notes">
        {!target && <li className="err">Path points outside this app.</li>}
        {parsed.errors.length > 0 && (
          <li className="err">Fix before sending: {parsed.errors.join('; ')}</li>
        )}
        {redacted > 0 && (
          <li className="warn">
            {redacted === 1 ? '1 redacted value (***), replace it' : `${redacted} redacted values (***), replace them`} before sending.
            The antiforgery token (__RequestVerificationToken) is always redacted by default, so Razor/MVC
            form posts return 400 unless you paste a fresh one.
          </li>
        )}
        {draft.originMismatch && (
          <li className="warn">Captured on {draft.originMismatch}, but the replay goes to {window.location.origin}.</li>
        )}
        {draft.bodyDisabledReason && <li>{draft.bodyDisabledReason} The replay goes out without a body.</li>}
        {parsed.dropped.length > 0 && (
          <li>Not sent, the browser sets these itself: {parsed.dropped.join(', ')}.</li>
        )}
        <li>
          Excluded headers like Authorization weren't stored, add them back by hand. Cookies come from this browser.
        </li>
      </ul>

      <h2 className="section-title">Headers</h2>
      <textarea
        className="replay-text"
        rows={Math.min(14, Math.max(4, headerText.split('\n').length + 1))}
        value={headerText}
        spellCheck={false}
        onChange={(e) => setHeaderText(e.target.value)}
        onKeyDown={stop}
      />

      <h2 className="section-title">Body</h2>
      <textarea
        className="replay-text"
        rows={8}
        value={noBody ? '' : body}
        disabled={noBody}
        placeholder={!sendsBody(method) ? `${method} requests don't send a body` : draft.bodyDisabledReason ?? ''}
        spellCheck={false}
        onChange={(e) => setBody(e.target.value)}
        onKeyDown={stop}
      />
    </div>
  );
}

function ReplayResult({ result, original, onOpen }: { result: Result; original: RequestEntry; onOpen: (ref: DetailRef) => void }) {
  if (result.state === 'sending') return <div className="replay-result dim">Sending…</div>;
  if (result.state === 'waiting') return <div className="replay-result dim">Sent, waiting for the recorded entry…</div>;

  if (result.state === 'missing') {
    return (
      <div className="replay-result">
        <div className="title">Not recorded</div>
        <div className="dim">
          Nothing showed up within {POLL_FOR_MS / 1000} s. The path may be excluded, a static file, or not
          covered by the dashboard.
        </div>
        {result.error
          ? <div className="err">{result.error}</div>
          : <div className="dim">Response: {result.status === 0 ? 'redirect (not followed)' : result.status}</div>}
      </div>
    );
  }

  const rows = compareRows(original, result.replay);
  return (
    <div className="replay-result">
      <div className="replay-compare">
        <span />
        <span className="dim">Original</span>
        <span className="dim">Replay</span>
        <span />
        {rows.map((r) => (
          <span key={r.label} style={{ display: 'contents' }}>
            <span className="dim">{r.label}</span>
            {r.label === 'Status' ? (
              <>
                <span><StatusCode code={r.before} /></span>
                <span><StatusCode code={r.after} /></span>
              </>
            ) : r.label === 'Duration' ? (
              <>
                <span>{fmtDuration(r.before)}</span>
                <span>{fmtDuration(r.after)}</span>
              </>
            ) : (
              <>
                <span>{r.before}</span>
                <span>{r.after}</span>
              </>
            )}
            <span className={`delta ${r.tone}`}>{r.delta}</span>
          </span>
        ))}
      </div>
      <button className="link-btn" onClick={() => onOpen({ kind: 'request', id: result.replay.id })}>
        open replay →
      </button>
    </div>
  );
}
