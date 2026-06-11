import { useEffect, useMemo, useState } from 'react';
import { api } from '../api';
import type { ExceptionEntry, LogEntry, RequestEntry, SqlQueryEntry } from '../types';
import { fmtBytes, fmtDateTime, fmtDuration, prettyBody } from '../format';
import { CodeBlock, CopyButton, LevelTag, MethodTag, StatusCode } from '../ui';
import { highlightSql } from '../sql';
import { toCurl } from '../curl';

export type DetailRef =
  | { kind: 'request'; id: string }
  | { kind: 'query'; id: string }
  | { kind: 'log'; id: string }
  | { kind: 'exception'; id: string };

function KV({ rows }: { rows: [string, React.ReactNode][] }) {
  return (
    <dl className="kv">
      {rows
        .filter(([, v]) => v !== null && v !== undefined && v !== '')
        .map(([k, v]) => (
          <span key={k} style={{ display: 'contents' }}>
            <dt>{k}</dt>
            <dd>{v}</dd>
          </span>
        ))}
    </dl>
  );
}

function Section({ title, children }: { title?: string; children: React.ReactNode }) {
  return (
    <div className="detail-section">
      {title && <h2 className="section-title">{title}</h2>}
      {children}
    </div>
  );
}

function HeadersBlock({ headers }: { headers: Record<string, string> | null | undefined }) {
  const entries = Object.entries(headers ?? {});
  if (entries.length === 0) return <div className="state-box"><div>No headers captured</div></div>;
  const text = entries.map(([k, v]) => `${k}: ${v}`).join('\n');
  return <CodeBlock text={text} />;
}

function Tabs<T extends string>({
  tabs,
  active,
  onSelect,
}: {
  tabs: { id: T; label: string; n?: number }[];
  active: T;
  onSelect: (id: T) => void;
}) {
  return (
    <div className="dtabs">
      {tabs.map((t) => (
        <button key={t.id} className={`dtab${t.id === active ? ' active' : ''}`} onClick={() => onSelect(t.id)}>
          {t.label}
          {t.n !== undefined && t.n > 0 && <span className="n">{t.n}</span>}
        </button>
      ))}
    </div>
  );
}

// Duplicate-query grouping: parameterized SQL repeats verbatim when the same
// statement runs in a loop, which is the classic N+1 shape.
function findNPlusOne(queries: SqlQueryEntry[]): { query: string; count: number } | null {
  const counts = new Map<string, number>();
  for (const q of queries) counts.set(q.query, (counts.get(q.query) ?? 0) + 1);
  let worst: { query: string; count: number } | null = null;
  for (const [query, count] of counts) {
    if (count >= 3 && (!worst || count > worst.count)) worst = { query, count };
  }
  return worst;
}

type ReqTab = 'summary' | 'headers' | 'request' | 'response' | 'sql' | 'logs';

function RequestDetail({ entry, onOpen }: { entry: RequestEntry; onOpen: (ref: DetailRef) => void }) {
  const [tab, setTab] = useState<ReqTab>('summary');
  const queries = entry.sqlQueries ?? [];
  const logs = entry.logs ?? [];
  const nPlusOne = useMemo(() => findNPlusOne(queries), [queries]);
  const sqlTotal = queries.reduce((s, q) => s + q.executionTimeMs, 0);

  const tabs: { id: ReqTab; label: string; n?: number }[] = [
    { id: 'summary', label: 'Summary' },
    { id: 'headers', label: 'Headers', n: Object.keys(entry.headers ?? {}).length },
    { id: 'request', label: 'Request' },
    { id: 'response', label: 'Response' },
    { id: 'sql', label: 'SQL', n: queries.length },
    { id: 'logs', label: 'Logs', n: logs.length },
  ];

  return (
    <>
      <Tabs tabs={tabs} active={tab} onSelect={setTab} />
      <div className="detail-body">
        {tab === 'summary' && (
          <>
            <KV
              rows={[
                ['Status', <StatusCode code={entry.statusCode} />],
                ['Duration', fmtDuration(entry.executionTimeMs)],
                ['SQL time', queries.length > 0 ? `${fmtDuration(sqlTotal)} across ${queries.length} queries` : null],
                ['Time', fmtDateTime(entry.timestamp)],
                ['URL', entry.url],
                ['Query string', entry.queryString],
                ['Client IP', entry.ipAddress],
                ['User agent', entry.userAgent],
                ['Protocol', entry.protocol],
                ['Request size', entry.requestSize > 0 ? fmtBytes(entry.requestSize) : null],
                ['Response size', entry.responseSize > 0 ? fmtBytes(entry.responseSize) : null],
                ['Request ID', entry.requestId],
              ]}
            />
            {entry.exception && (
              <Section title="Exception">
                <CodeBlock text={entry.exception} />
              </Section>
            )}
          </>
        )}

        {tab === 'headers' && <HeadersBlock headers={entry.headers} />}

        {tab === 'request' && (
          entry.requestBody
            ? <CodeBlock text={prettyBody(entry.requestBody)} />
            : <div className="state-box"><div>No request body{entry.method === 'GET' ? ' (GET request)' : ' captured'}</div></div>
        )}

        {tab === 'response' && (
          entry.responseBody
            ? <CodeBlock text={prettyBody(entry.responseBody)} />
            : <div className="state-box"><div>No response body captured — enable <code>LogResponseBodies</code> in config</div></div>
        )}

        {tab === 'sql' && (
          <>
            {nPlusOne && (
              <div className="banner">
                <div>
                  <div className="b-title">Possible N+1 — identical query ran {nPlusOne.count}×</div>
                  <div className="b-sub">{nPlusOne.query.slice(0, 120)}{nPlusOne.query.length > 120 ? '…' : ''}</div>
                </div>
              </div>
            )}
            {queries.length === 0
              ? <div className="state-box"><div>No queries ran during this request</div></div>
              : queries.map((q) => (
                  <div key={q.id} className="sub-row" onClick={() => onOpen({ kind: 'query', id: q.id })}>
                    <span className="grow">{q.query}</span>
                    <span className={q.isSlowQuery ? 'level warning' : undefined}>{fmtDuration(q.executionTimeMs)}</span>
                  </div>
                ))}
          </>
        )}

        {tab === 'logs' && (
          logs.length === 0
            ? <div className="state-box"><div>No logs written during this request</div></div>
            : logs.map((l) => (
                <div key={l.id} className="sub-row" onClick={() => onOpen({ kind: 'log', id: l.id })}>
                  <LevelTag level={l.level} />
                  <span className="grow">{l.message}</span>
                </div>
              ))
        )}
      </div>
    </>
  );
}

function ParentRequestLink({ requestId, onOpen }: { requestId?: string | null; onOpen: (ref: DetailRef) => void }) {
  const [state, setState] = useState<'idle' | 'loading' | 'none'>('idle');

  if (!requestId) return null;

  const open = async () => {
    setState('loading');
    try {
      const res = await api.requests({ page: 1, pageSize: 1, requestId });
      if (res.items.length > 0) onOpen({ kind: 'request', id: res.items[0].id });
      else setState('none');
    } catch {
      setState('none');
    }
  };

  if (state === 'none') return <span style={{ color: 'var(--faint)' }}>request not found</span>;
  return (
    <button className="link-btn" onClick={open} disabled={state === 'loading'}>
      {state === 'loading' ? 'opening…' : 'open request →'}
    </button>
  );
}

function QueryDetail({ entry, onOpen }: { entry: SqlQueryEntry; onOpen: (ref: DetailRef) => void }) {
  return (
    <div className="detail-body">
      <KV
        rows={[
          ['Duration', <span className={entry.isSlowQuery ? 'level warning' : undefined}>{fmtDuration(entry.executionTimeMs)}{entry.isSlowQuery ? ' — slow' : ''}</span>],
          ['Time', fmtDateTime(entry.timestamp)],
          ['Rows affected', entry.rowsAffected > 0 ? entry.rowsAffected : null],
          ['Database', entry.database],
          ['Succeeded', entry.isSuccessful ? 'yes' : 'no'],
          ['Request', <ParentRequestLink requestId={entry.requestId} onOpen={onOpen} />],
        ]}
      />
      <Section title="Query">
        <CodeBlock text={entry.query}>{highlightSql(entry.query)}</CodeBlock>
      </Section>
      {Object.keys(entry.parameters ?? {}).length > 0 && (
        <Section title="Parameters">
          <CodeBlock text={JSON.stringify(entry.parameters, null, 2)} />
        </Section>
      )}
      {entry.error && (
        <Section title="Error">
          <CodeBlock text={entry.error} />
        </Section>
      )}
      {entry.stackTrace && (
        <Section title="Stack trace">
          <CodeBlock text={entry.stackTrace} />
        </Section>
      )}
    </div>
  );
}

function LogDetail({ entry, onOpen }: { entry: LogEntry; onOpen: (ref: DetailRef) => void }) {
  return (
    <div className="detail-body">
      <KV
        rows={[
          ['Level', <LevelTag level={entry.level} />],
          ['Time', fmtDateTime(entry.timestamp)],
          ['Category', entry.category],
          ['Tag', entry.tag],
          ['Thread', entry.threadId],
          ['Machine', entry.machineName],
          ['Request', <ParentRequestLink requestId={entry.requestId} onOpen={onOpen} />],
        ]}
      />
      <Section title="Message">
        <CodeBlock text={entry.message} />
      </Section>
      {Object.keys(entry.properties ?? {}).length > 0 && (
        <Section title="Properties">
          <CodeBlock text={JSON.stringify(entry.properties, null, 2)} />
        </Section>
      )}
      {entry.exception && (
        <Section title="Exception">
          <CodeBlock text={entry.exception} />
        </Section>
      )}
      {entry.stackTrace && (
        <Section title="Stack trace">
          <CodeBlock text={entry.stackTrace} />
        </Section>
      )}
    </div>
  );
}

function ExceptionDetail({ entry, onOpen }: { entry: ExceptionEntry; onOpen: (ref: DetailRef) => void }) {
  return (
    <div className="detail-body">
      <KV
        rows={[
          ['Type', entry.exceptionType],
          ['Time', fmtDateTime(entry.timestamp)],
          ['Route', entry.method && entry.path ? `${entry.method} ${entry.path}` : entry.path],
          ['Source', entry.source],
          ['Target site', entry.targetSite],
          ['Client IP', entry.ipAddress],
          ['Request', <ParentRequestLink requestId={entry.requestId} onOpen={onOpen} />],
        ]}
      />
      <Section title="Message">
        <CodeBlock text={entry.message} />
      </Section>
      {entry.stackTrace && (
        <Section title="Stack trace">
          <CodeBlock text={entry.stackTrace} />
        </Section>
      )}
      {entry.innerException && (
        <Section title={`Inner exception — ${entry.innerException.exceptionType ?? 'unknown type'}`}>
          <CodeBlock
            text={entry.innerException.message + (entry.innerException.stackTrace ? `\n\n${entry.innerException.stackTrace}` : '')}
          />
        </Section>
      )}
      {entry.requestBody && (
        <Section title="Request body">
          <CodeBlock text={prettyBody(entry.requestBody)} />
        </Section>
      )}
    </div>
  );
}

const fetchers = {
  request: api.request,
  query: api.query,
  log: api.log,
  exception: api.exception,
};

export function DetailPanel({
  refs,
  onClose,
  onOpen,
}: {
  refs: DetailRef;
  onClose: () => void;
  onOpen: (ref: DetailRef) => void;
}) {
  const [entry, setEntry] = useState<RequestEntry | SqlQueryEntry | LogEntry | ExceptionEntry | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setEntry(null);
    setError(null);
    fetchers[refs.kind](refs.id)
      .then((e) => setEntry(e as typeof entry))
      .catch((e) => setError(e instanceof Error ? e.message : String(e)));
  }, [refs]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose]);

  let title: React.ReactNode = '';
  let meta: React.ReactNode = null;
  let actions: React.ReactNode = null;
  let body: React.ReactNode = null;

  if (entry) {
    switch (refs.kind) {
      case 'request': {
        const r = entry as RequestEntry;
        title = <><MethodTag method={r.method} /> {r.path}</>;
        meta = (
          <>
            <StatusCode code={r.statusCode} />
            <span>{fmtDuration(r.executionTimeMs)}</span>
            <span>{fmtDateTime(r.timestamp)}</span>
          </>
        );
        actions = <CopyButton text={toCurl(r)} label="copy as cURL" />;
        body = <RequestDetail entry={r} onOpen={onOpen} />;
        break;
      }
      case 'query': {
        const q = entry as SqlQueryEntry;
        title = 'SQL query';
        meta = (
          <>
            <span className={q.isSlowQuery ? 'level warning' : undefined}>{fmtDuration(q.executionTimeMs)}</span>
            <span>{fmtDateTime(q.timestamp)}</span>
          </>
        );
        body = <QueryDetail entry={q} onOpen={onOpen} />;
        break;
      }
      case 'log': {
        const l = entry as LogEntry;
        title = l.message.length > 80 ? `${l.message.slice(0, 80)}…` : l.message;
        meta = (
          <>
            <LevelTag level={l.level} />
            <span>{fmtDateTime(l.timestamp)}</span>
          </>
        );
        body = <LogDetail entry={l} onOpen={onOpen} />;
        break;
      }
      case 'exception': {
        const x = entry as ExceptionEntry;
        title = x.exceptionType ?? 'Exception';
        meta = (
          <>
            <span className="level error">EXCEPTION</span>
            <span>{fmtDateTime(x.timestamp)}</span>
          </>
        );
        body = <ExceptionDetail entry={x} onOpen={onOpen} />;
        break;
      }
    }
  }

  return (
    <>
      <div className="overlay" onClick={onClose} />
      <aside className="detail">
        <div className="detail-head">
          <div className="row1">
            <span className="title">{title}</span>
            {actions}
            <button className="detail-close" onClick={onClose} aria-label="Close">✕</button>
          </div>
          {meta && <div className="meta">{meta}</div>}
        </div>
        {error && <div className="state-box error"><div className="title">{error}</div></div>}
        {!entry && !error && (
          <div className="detail-body">
            <div className="skeleton-rows">
              <div className="skeleton" />
              <div className="skeleton" style={{ width: '70%' }} />
              <div className="skeleton" style={{ width: '55%' }} />
            </div>
          </div>
        )}
        {body}
      </aside>
    </>
  );
}
