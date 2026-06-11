import { useEffect, useState } from 'react';
import { api } from '../api';
import type { ExceptionEntry, LogEntry, RequestEntry, SqlQueryEntry } from '../types';
import { fmtBytes, fmtDateTime, fmtDuration, prettyBody } from '../format';
import { LevelTag, MethodTag, StatusCode } from '../ui';

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

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="detail-section">
      <h2 className="section-title">{title}</h2>
      {children}
    </div>
  );
}

function HeadersBlock({ headers }: { headers: Record<string, string> }) {
  const entries = Object.entries(headers);
  if (entries.length === 0) return null;
  return (
    <Section title="Headers">
      <pre className="code">{entries.map(([k, v]) => `${k}: ${v}`).join('\n')}</pre>
    </Section>
  );
}

function RequestDetail({ entry, onOpen }: { entry: RequestEntry; onOpen: (ref: DetailRef) => void }) {
  return (
    <>
      <KV
        rows={[
          ['Status', <StatusCode code={entry.statusCode} />],
          ['Duration', fmtDuration(entry.executionTimeMs)],
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
      <HeadersBlock headers={entry.headers} />
      {entry.requestBody && (
        <Section title="Request body">
          <pre className="code">{prettyBody(entry.requestBody)}</pre>
        </Section>
      )}
      {entry.responseBody && (
        <Section title="Response body">
          <pre className="code">{prettyBody(entry.responseBody)}</pre>
        </Section>
      )}
      {entry.exception && (
        <Section title="Exception">
          <pre className="code">{entry.exception}</pre>
        </Section>
      )}
      {entry.sqlQueries.length > 0 && (
        <Section title={`SQL queries (${entry.sqlQueries.length})`}>
          {entry.sqlQueries.map((q) => (
            <div key={q.id} className="sub-row" style={{ cursor: 'pointer' }} onClick={() => onOpen({ kind: 'query', id: q.id })}>
              <span className="grow">{q.query}</span>
              <span className={q.isSlowQuery ? 'slow-mark' : undefined}>{fmtDuration(q.executionTimeMs)}</span>
            </div>
          ))}
        </Section>
      )}
      {entry.logs.length > 0 && (
        <Section title={`Logs (${entry.logs.length})`}>
          {entry.logs.map((l) => (
            <div key={l.id} className="sub-row" style={{ cursor: 'pointer' }} onClick={() => onOpen({ kind: 'log', id: l.id })}>
              <LevelTag level={l.level} />
              <span className="grow">{l.message}</span>
            </div>
          ))}
        </Section>
      )}
    </>
  );
}

function QueryDetail({ entry }: { entry: SqlQueryEntry }) {
  return (
    <>
      <KV
        rows={[
          ['Duration', <span className={entry.isSlowQuery ? 'slow-mark' : undefined}>{fmtDuration(entry.executionTimeMs)}{entry.isSlowQuery ? ' (slow)' : ''}</span>],
          ['Time', fmtDateTime(entry.timestamp)],
          ['Rows affected', entry.rowsAffected >= 0 ? entry.rowsAffected : null],
          ['Database', entry.database],
          ['Command type', entry.commandType],
          ['Succeeded', entry.isSuccessful ? 'yes' : 'no'],
          ['Request ID', entry.requestId],
        ]}
      />
      <Section title="Query">
        <pre className="code">{entry.query}</pre>
      </Section>
      {Object.keys(entry.parameters).length > 0 && (
        <Section title="Parameters">
          <pre className="code">{JSON.stringify(entry.parameters, null, 2)}</pre>
        </Section>
      )}
      {entry.error && (
        <Section title="Error">
          <pre className="code">{entry.error}</pre>
        </Section>
      )}
      {entry.stackTrace && (
        <Section title="Stack trace">
          <pre className="code">{entry.stackTrace}</pre>
        </Section>
      )}
    </>
  );
}

function LogDetail({ entry }: { entry: LogEntry }) {
  return (
    <>
      <KV
        rows={[
          ['Level', <LevelTag level={entry.level} />],
          ['Time', fmtDateTime(entry.timestamp)],
          ['Category', entry.category],
          ['Tag', entry.tag],
          ['Thread', entry.threadId],
          ['Machine', entry.machineName],
          ['Request ID', entry.requestId],
        ]}
      />
      <Section title="Message">
        <pre className="code">{entry.message}</pre>
      </Section>
      {Object.keys(entry.properties).length > 0 && (
        <Section title="Properties">
          <pre className="code">{JSON.stringify(entry.properties, null, 2)}</pre>
        </Section>
      )}
      {entry.exception && (
        <Section title="Exception">
          <pre className="code">{entry.exception}</pre>
        </Section>
      )}
      {entry.stackTrace && (
        <Section title="Stack trace">
          <pre className="code">{entry.stackTrace}</pre>
        </Section>
      )}
    </>
  );
}

function ExceptionDetail({ entry }: { entry: ExceptionEntry }) {
  return (
    <>
      <KV
        rows={[
          ['Type', entry.exceptionType],
          ['Time', fmtDateTime(entry.timestamp)],
          ['Route', entry.method && entry.path ? `${entry.method} ${entry.path}` : entry.path],
          ['Source', entry.source],
          ['Target site', entry.targetSite],
          ['Client IP', entry.ipAddress],
          ['Request ID', entry.requestId],
        ]}
      />
      <Section title="Message">
        <pre className="code">{entry.message}</pre>
      </Section>
      {entry.stackTrace && (
        <Section title="Stack trace">
          <pre className="code">{entry.stackTrace}</pre>
        </Section>
      )}
      {entry.innerException && (
        <Section title={`Inner: ${entry.innerException.exceptionType ?? 'exception'}`}>
          <pre className="code">
            {entry.innerException.message}
            {entry.innerException.stackTrace ? `\n\n${entry.innerException.stackTrace}` : ''}
          </pre>
        </Section>
      )}
      {entry.requestBody && (
        <Section title="Request body">
          <pre className="code">{prettyBody(entry.requestBody)}</pre>
        </Section>
      )}
    </>
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
  let body: React.ReactNode = null;
  if (entry) {
    switch (refs.kind) {
      case 'request': {
        const r = entry as RequestEntry;
        title = (
          <>
            <MethodTag method={r.method} /> {r.path}
          </>
        );
        body = <RequestDetail entry={r} onOpen={onOpen} />;
        break;
      }
      case 'query':
        title = 'SQL query';
        body = <QueryDetail entry={entry as SqlQueryEntry} />;
        break;
      case 'log':
        title = 'Log entry';
        body = <LogDetail entry={entry as LogEntry} />;
        break;
      case 'exception':
        title = (entry as ExceptionEntry).exceptionType ?? 'Exception';
        body = <ExceptionDetail entry={entry as ExceptionEntry} />;
        break;
    }
  }

  return (
    <>
      <div className="overlay" onClick={onClose} />
      <aside className="detail">
        <div className="detail-head">
          <span className="title">{title}</span>
          <button className="detail-close" onClick={onClose} aria-label="Close">
            ✕
          </button>
        </div>
        <div className="detail-body">
          {error && <div className="state-box error"><div className="title">{error}</div></div>}
          {!entry && !error && <div className="skeleton-rows"><div className="skeleton" /><div className="skeleton" style={{ width: '70%' }} /><div className="skeleton" style={{ width: '55%' }} /></div>}
          {body}
        </div>
      </aside>
    </>
  );
}
