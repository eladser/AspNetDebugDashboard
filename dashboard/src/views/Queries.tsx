import { api } from '../api';
import { fmtDuration, fmtTime } from '../format';
import { EmptyState, ErrorState, Pager, SearchBox, Skeleton } from '../ui';
import { useList } from '../useList';
import type { DetailRef } from './Detail';

export function Queries({ tick, onOpen }: { tick: number; onOpen: (ref: DetailRef) => void }) {
  const { query, patch, setPage, data, error, loading, reload } = useList(api.queries, tick);

  return (
    <>
      <div className="filters">
        <SearchBox
          value={query.search ?? ''}
          onChange={(search) => patch({ search: search || undefined })}
          placeholder="Search SQL…"
        />
        {data && <span className="result-count">{data.totalCount.toLocaleString()} total</span>}
      </div>

      {error ? (
        <ErrorState message={error} onRetry={reload} />
      ) : loading && !data ? (
        <div className="table-wrap"><Skeleton /></div>
      ) : !data || data.items.length === 0 ? (
        <EmptyState
          title="No SQL queries captured"
          hint={query.search
            ? 'Nothing matches the current search.'
            : <>Wire up the EF Core interceptor with <code>options.AddDebugDashboard()</code> to capture queries.</>}
        />
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Query</th>
                <th className="num fit">Duration</th>
                <th className="num fit">Rows</th>
                <th className="fit">Time</th>
              </tr>
            </thead>
            <tbody>
              {data.items.map((q) => (
                <tr key={q.id} onClick={() => onOpen({ kind: 'query', id: q.id })}>
                  <td className="primary" style={{ maxWidth: 560 }} title={q.query}>
                    {!q.isSuccessful && <span className="status s5">✕ </span>}
                    {q.isSlowQuery && <span className="slow-mark">⏱ </span>}
                    {q.query}
                  </td>
                  <td className={`num fit${q.isSlowQuery ? ' slow-mark' : ''}`}>{fmtDuration(q.executionTimeMs)}</td>
                  <td className="num dim fit">{q.rowsAffected >= 0 ? q.rowsAffected : ''}</td>
                  <td className="dim fit">{fmtTime(q.timestamp)}</td>
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
