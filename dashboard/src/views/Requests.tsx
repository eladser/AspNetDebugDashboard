import { useMemo, useRef } from 'react';
import { api } from '../api';
import { Chip, Duration, EmptyState, ErrorState, FootBar, MethodTag, RelTime, SearchBox, Skeleton, SortHeader, StatusCode } from '../ui';
import { useList, useRowNav } from '../useList';
import type { DetailRef } from './Detail';

const METHODS = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE'];
const SETUP = `builder.Services.AddDebugDashboard();
app.UseDebugDashboard();`;

export function Requests({
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
  const { query, patch, toggleSort, setPage, data, error, loading, reload } = useList(api.requests, tick);
  const items = data?.items ?? [];
  const { sel, setSel } = useRowNav(items, (r) => onOpen({ kind: 'request', id: r.id }), navEnabled);
  const maxMs = useMemo(() => Math.max(...items.map((r) => r.executionTimeMs), 1), [items]);
  const slowRef = useRef(2000);

  const hasFilters = !!(query.search || query.method || query.statusCode || query.isSuccessful !== undefined || query.minExecutionTime);

  return (
    <>
      <div className="filters">
        <SearchBox
          inputRef={searchRef}
          value={query.search ?? ''}
          onChange={(search) => patch({ search: search || undefined })}
          placeholder="Filter by path or URL…"
        />
        <select
          className="select"
          value={query.method ?? ''}
          onChange={(e) => patch({ method: e.target.value || undefined })}
        >
          <option value="">Method</option>
          {METHODS.map((m) => <option key={m}>{m}</option>)}
        </select>
        <select
          className="select"
          value={query.statusCode ?? ''}
          onChange={(e) => patch({ statusCode: e.target.value ? Number(e.target.value) : undefined })}
        >
          <option value="">Status</option>
          {[200, 201, 204, 301, 302, 400, 401, 403, 404, 500].map((s) => (
            <option key={s} value={s}>{s}</option>
          ))}
        </select>
        <Chip
          label="Failed only"
          tone="err"
          on={query.isSuccessful === false}
          onClick={() => patch({ isSuccessful: query.isSuccessful === false ? undefined : false })}
        />
        <Chip
          label="Slow only"
          tone="warn"
          on={!!query.minExecutionTime}
          onClick={() => patch({ minExecutionTime: query.minExecutionTime ? undefined : slowRef.current })}
        />
      </div>

      {error ? (
        <ErrorState message={error} onRetry={reload} />
      ) : loading && !data ? (
        <div className="table-wrap"><Skeleton /></div>
      ) : items.length === 0 ? (
        <EmptyState
          title="No requests captured"
          hint={hasFilters
            ? 'Nothing matches the current filters.'
            : 'Hit any endpoint in your app and it shows up here. If this is a fresh install:'}
          snippet={hasFilters ? undefined : SETUP}
        />
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th className="fit">Method</th>
                <th>Path</th>
                <SortHeader label="Status" field="statuscode" query={query} onSort={toggleSort} className="fit" />
                <SortHeader label="Duration" field="executiontimems" query={query} onSort={toggleSort} className="num fit" />
                <th className="num fit">SQL</th>
                <SortHeader label="When" field="timestamp" query={query} onSort={toggleSort} className="fit" />
              </tr>
            </thead>
            <tbody>
              {items.map((r, i) => (
                <tr
                  key={r.id}
                  className={i === sel ? 'sel' : ''}
                  onClick={() => { setSel(i); onOpen({ kind: 'request', id: r.id }); }}
                >
                  <td className="fit"><MethodTag method={r.method} /></td>
                  <td className="primary" title={r.url}>{r.path}{r.queryString}</td>
                  <td className="fit"><StatusCode code={r.statusCode} /></td>
                  <td className="num fit">
                    <Duration ms={r.executionTimeMs} max={maxMs} slow={r.executionTimeMs >= slowRef.current} />
                  </td>
                  <td className="num dim fit">{r.sqlQueries?.length || ''}</td>
                  <td className="dim fit"><RelTime iso={r.timestamp} tick={tick} /></td>
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
