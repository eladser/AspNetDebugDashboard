import { api } from '../api';
import { fmtTime } from '../format';
import { EmptyState, ErrorState, LevelTag, Pager, SearchBox, Skeleton } from '../ui';
import { useList } from '../useList';
import type { DetailRef } from './Detail';

const LEVELS = ['Trace', 'Debug', 'Info', 'Warning', 'Error', 'Critical'];

export function Logs({ tick, onOpen }: { tick: number; onOpen: (ref: DetailRef) => void }) {
  const { query, patch, setPage, data, error, loading, reload } = useList(api.logs, tick);

  return (
    <>
      <div className="filters">
        <SearchBox
          value={query.search ?? ''}
          onChange={(search) => patch({ search: search || undefined })}
          placeholder="Search messages…"
        />
        <select
          className="select"
          value={query.level ?? ''}
          onChange={(e) => patch({ level: e.target.value || undefined })}
        >
          <option value="">All levels</option>
          {LEVELS.map((l) => (
            <option key={l}>{l}</option>
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
          title="No log entries"
          hint={query.search || query.level
            ? 'Nothing matches the current filters.'
            : <>Inject <code>IDebugLogger</code> and call <code>LogAsync(...)</code> to write entries.</>}
        />
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th className="fit">Level</th>
                <th>Message</th>
                <th className="fit">Category</th>
                <th className="fit">Time</th>
              </tr>
            </thead>
            <tbody>
              {data.items.map((l) => (
                <tr key={l.id} onClick={() => onOpen({ kind: 'log', id: l.id })}>
                  <td className="fit"><LevelTag level={l.level} /></td>
                  <td className="primary" style={{ maxWidth: 560 }} title={l.message}>{l.message}</td>
                  <td className="dim fit">{l.category ?? l.tag ?? ''}</td>
                  <td className="dim fit">{fmtTime(l.timestamp)}</td>
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
