import { api } from '../api';
import { EmptyState, ErrorState, FootBar, LevelTag, RelTime, SearchBox, Skeleton, SortHeader } from '../ui';
import { useList, useRowNav } from '../useList';
import type { DetailRef } from './Detail';

const LEVELS = ['Trace', 'Debug', 'Info', 'Warning', 'Error', 'Critical'];
const SETUP = `public class OrderService(IDebugLogger log)
{
    await log.LogInfoAsync("Order created",
        properties: new() { ["orderId"] = order.Id });
}`;

export function Logs({
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
  const { query, patch, toggleSort, setPage, data, error, loading, reload } = useList(api.logs, tick);
  const items = data?.items ?? [];
  const { sel, setSel } = useRowNav(items, (l) => onOpen({ kind: 'log', id: l.id }), navEnabled);

  return (
    <>
      <div className="filters">
        <SearchBox
          inputRef={searchRef}
          value={query.search ?? ''}
          onChange={(search) => patch({ search: search || undefined })}
          placeholder="Filter by message…"
        />
        <select
          className="select"
          value={query.level ?? ''}
          onChange={(e) => patch({ level: e.target.value || undefined })}
        >
          <option value="">Level</option>
          {LEVELS.map((l) => <option key={l}>{l}</option>)}
        </select>
      </div>

      {error ? (
        <ErrorState message={error} onRetry={reload} />
      ) : loading && !data ? (
        <div className="table-wrap"><Skeleton /></div>
      ) : items.length === 0 ? (
        <EmptyState
          title="No log entries"
          hint={query.search || query.level
            ? 'Nothing matches the current filters.'
            : 'Inject IDebugLogger anywhere and write structured entries:'}
          snippet={query.search || query.level ? undefined : SETUP}
        />
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th className="fit">Level</th>
                <th>Message</th>
                <th className="fit">Category</th>
                <SortHeader label="When" field="timestamp" query={query} onSort={toggleSort} className="fit" />
              </tr>
            </thead>
            <tbody>
              {items.map((l, i) => (
                <tr
                  key={l.id}
                  className={i === sel ? 'sel' : ''}
                  onClick={() => { setSel(i); onOpen({ kind: 'log', id: l.id }); }}
                >
                  <td className="fit"><LevelTag level={l.level} /></td>
                  <td className="primary" style={{ maxWidth: 600 }} title={l.message}>{l.message}</td>
                  <td className="dim fit">{l.category ?? l.tag ?? ''}</td>
                  <td className="dim fit"><RelTime iso={l.timestamp} tick={tick} /></td>
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
