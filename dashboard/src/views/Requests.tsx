import { api } from '../api';
import { fmtDuration, fmtTime } from '../format';
import { EmptyState, ErrorState, MethodTag, Pager, SearchBox, Skeleton, StatusCode } from '../ui';
import { useList } from '../useList';
import type { DetailRef } from './Detail';

const METHODS = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE'];

export function Requests({ tick, onOpen }: { tick: number; onOpen: (ref: DetailRef) => void }) {
  const { query, patch, setPage, data, error, loading, reload } = useList(api.requests, tick);

  return (
    <>
      <div className="filters">
        <SearchBox
          value={query.search ?? ''}
          onChange={(search) => patch({ search: search || undefined })}
          placeholder="Search path, url, body…"
        />
        <select
          className="select"
          value={query.method ?? ''}
          onChange={(e) => patch({ method: e.target.value || undefined })}
        >
          <option value="">All methods</option>
          {METHODS.map((m) => (
            <option key={m}>{m}</option>
          ))}
        </select>
        <select
          className="select"
          value={query.statusCode ?? ''}
          onChange={(e) => patch({ statusCode: e.target.value ? Number(e.target.value) : undefined })}
        >
          <option value="">All statuses</option>
          {[200, 201, 204, 301, 302, 400, 401, 403, 404, 500].map((s) => (
            <option key={s} value={s}>{s}</option>
          ))}
        </select>
        {data && <span className="result-count">{data.totalCount.toLocaleString()} total</span>}
      </div>

      {error ? (
        <ErrorState message={error} onRetry={reload} />
      ) : loading && !data ? (
        <div className="table-wrap"><Skeleton /></div>
      ) : !data || data.items.length === 0 ? (
        <EmptyState
          title="No requests captured"
          hint={query.search || query.method || query.statusCode
            ? 'Nothing matches the current filters.'
            : <>Hit any endpoint in your app and it shows up here.</>}
        />
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th className="fit">Method</th>
                <th>Path</th>
                <th className="fit">Status</th>
                <th className="num fit">Duration</th>
                <th className="num fit">SQL</th>
                <th className="fit">Time</th>
              </tr>
            </thead>
            <tbody>
              {data.items.map((r) => (
                <tr key={r.id} onClick={() => onOpen({ kind: 'request', id: r.id })}>
                  <td className="fit"><MethodTag method={r.method} /></td>
                  <td className="primary" title={r.url}>{r.path}{r.queryString}</td>
                  <td className="fit"><StatusCode code={r.statusCode} /></td>
                  <td className="num fit">{fmtDuration(r.executionTimeMs)}</td>
                  <td className="num dim fit">{r.sqlQueries.length || ''}</td>
                  <td className="dim fit">{fmtTime(r.timestamp)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {data && <Pager result={data} onPage={setPage} />}
    </>
  );
}
