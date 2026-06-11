import { api } from '../api';
import { fmtTime } from '../format';
import { EmptyState, ErrorState, MethodTag, Pager, SearchBox, Skeleton } from '../ui';
import { useList } from '../useList';
import type { DetailRef } from './Detail';

export function Exceptions({ tick, onOpen }: { tick: number; onOpen: (ref: DetailRef) => void }) {
  const { query, patch, setPage, data, error, loading, reload } = useList(api.exceptions, tick);

  return (
    <>
      <div className="filters">
        <SearchBox
          value={query.search ?? ''}
          onChange={(search) => patch({ search: search || undefined })}
          placeholder="Search messages, types…"
        />
        {data && <span className="result-count">{data.totalCount.toLocaleString()} total</span>}
      </div>

      {error ? (
        <ErrorState message={error} onRetry={reload} />
      ) : loading && !data ? (
        <div className="table-wrap"><Skeleton /></div>
      ) : !data || data.items.length === 0 ? (
        <EmptyState
          title="No exceptions"
          hint={query.search ? 'Nothing matches the current search.' : 'Good news — nothing has blown up.'}
        />
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th className="fit">Type</th>
                <th>Message</th>
                <th className="fit">Route</th>
                <th className="fit">Time</th>
              </tr>
            </thead>
            <tbody>
              {data.items.map((x) => (
                <tr key={x.id} onClick={() => onOpen({ kind: 'exception', id: x.id })}>
                  <td className="status s5 fit">{x.exceptionType?.split('.').pop() ?? 'Exception'}</td>
                  <td className="primary" style={{ maxWidth: 480 }} title={x.message}>{x.message}</td>
                  <td className="dim fit">
                    {x.method && <><MethodTag method={x.method} /> </>}
                    {x.path}
                  </td>
                  <td className="dim fit">{fmtTime(x.timestamp)}</td>
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
