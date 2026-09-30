import { api } from '../api';
import { EmptyState, ErrorState, FootBar, RelTime, SearchBox, Skeleton, SortHeader } from '../ui';
import { useList, useRowNav } from '../useList';
import type { DetailRef } from './Detail';

const SETUP = `using AspNetDebugDashboard;

DebugDump.Dump(order, "created order");`;

function fileLine(file?: string | null, line?: number) {
  if (!file) return '';
  const name = file.split(/[\\/]/).pop() ?? file;
  return line ? `${name}:${line}` : name;
}

export function Dumps({
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
  const { query, patch, toggleSort, setPage, data, error, loading, reload } = useList(api.dumps, tick);
  const items = data?.items ?? [];
  const { sel, setSel } = useRowNav(items, (d) => onOpen({ kind: 'dump', id: d.id }), navEnabled);

  return (
    <>
      <div className="filters">
        <SearchBox
          inputRef={searchRef}
          value={query.search ?? ''}
          onChange={(search) => patch({ search: search || undefined })}
          placeholder="Filter by label, type, or content…"
        />
      </div>

      {error ? (
        <ErrorState message={error} onRetry={reload} />
      ) : loading && !data ? (
        <div className="table-wrap"><Skeleton /></div>
      ) : items.length === 0 ? (
        <EmptyState
          title="No dumps captured"
          hint={query.search
            ? 'Nothing matches the current filter.'
            : 'Call DebugDump.Dump() anywhere to snapshot a variable:'}
          snippet={query.search ? undefined : SETUP}
        />
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Label</th>
                <th className="fit">Type</th>
                <th className="fit">Source</th>
                <SortHeader label="When" field="timestamp" query={query} onSort={toggleSort} className="fit" />
              </tr>
            </thead>
            <tbody>
              {items.map((d, i) => (
                <tr
                  key={d.id}
                  className={i === sel ? 'sel' : ''}
                  onClick={() => { setSel(i); onOpen({ kind: 'dump', id: d.id }); }}
                >
                  <td className="primary">{d.label ?? <span className="dim">(no label)</span>}</td>
                  <td className="dim fit" style={{ maxWidth: 240 }} title={d.typeName}>{d.typeName.split('.').pop()}</td>
                  <td className="dim fit">{fileLine(d.file, d.line)}</td>
                  <td className="dim fit"><RelTime iso={d.timestamp} tick={tick} /></td>
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
