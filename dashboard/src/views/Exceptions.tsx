import { api } from '../api';
import { EmptyState, ErrorState, FootBar, MethodTag, RelTime, SearchBox, Skeleton, SortHeader } from '../ui';
import { useList, useRowNav } from '../useList';
import type { DetailRef } from './Detail';

export function Exceptions({
  tick,
  navEnabled,
  searchRef,
  onOpen,
}: {
  tick: number;
  navEnabled: boolean;
  searchRef: React.RefObject<HTMLInputElement>;
  onOpen: (ref: DetailRef) => void;
}) {
  const { query, patch, toggleSort, setPage, data, error, loading, reload } = useList(api.exceptions, tick);
  const items = data?.items ?? [];
  const { sel, setSel } = useRowNav(items, (x) => onOpen({ kind: 'exception', id: x.id }), navEnabled);

  return (
    <>
      <div className="filters">
        <SearchBox
          inputRef={searchRef}
          value={query.search ?? ''}
          onChange={(search) => patch({ search: search || undefined })}
          placeholder="Filter by message or type…"
        />
      </div>

      {error ? (
        <ErrorState message={error} onRetry={reload} />
      ) : loading && !data ? (
        <div className="table-wrap"><Skeleton /></div>
      ) : items.length === 0 ? (
        <EmptyState
          title="No exceptions"
          hint={query.search ? 'Nothing matches the current search.' : 'Unhandled exceptions land here with full stack traces. None so far.'}
        />
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th className="fit">Type</th>
                <th>Message</th>
                <th className="fit">Route</th>
                <SortHeader label="When" field="timestamp" query={query} onSort={toggleSort} className="fit" />
              </tr>
            </thead>
            <tbody>
              {items.map((x, i) => (
                <tr
                  key={x.id}
                  className={i === sel ? 'sel' : ''}
                  onClick={() => { setSel(i); onOpen({ kind: 'exception', id: x.id }); }}
                >
                  <td className="status s5 fit">{x.exceptionType?.split('.').pop() ?? 'Exception'}</td>
                  <td className="primary" style={{ maxWidth: 520 }} title={x.message}>{x.message}</td>
                  <td className="dim fit">
                    {x.method && <><MethodTag method={x.method} /> </>}
                    {x.path}
                  </td>
                  <td className="dim fit"><RelTime iso={x.timestamp} tick={tick} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <FootBar result={data} onPage={setPage} />
    </>
  );
}
