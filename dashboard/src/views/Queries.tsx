import { useMemo } from 'react';
import { api } from '../api';
import { Chip, Duration, EmptyState, ErrorState, FootBar, RelTime, SearchBox, Skeleton, SortHeader } from '../ui';
import { useList, useRowNav } from '../useList';
import type { DetailRef } from './Detail';

const SETUP = `builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    options.UseSqlServer(connectionString);
    options.AddDebugDashboard(sp);
});`;

export function Queries({
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
  const { query, patch, toggleSort, setPage, data, error, loading, reload } = useList(api.queries, tick);
  const items = data?.items ?? [];
  const { sel, setSel } = useRowNav(items, (q) => onOpen({ kind: 'query', id: q.id }), navEnabled);
  const maxMs = useMemo(() => Math.max(...items.map((q) => q.executionTimeMs), 1), [items]);

  const hasFilters = !!(query.search || query.isSlowQuery !== undefined || query.isSuccessful !== undefined);

  return (
    <>
      <div className="filters">
        <SearchBox
          inputRef={searchRef}
          value={query.search ?? ''}
          onChange={(search) => patch({ search: search || undefined })}
          placeholder="Filter by SQL text…"
        />
        <Chip
          label="Slow only"
          tone="warn"
          on={query.isSlowQuery === true}
          onClick={() => patch({ isSlowQuery: query.isSlowQuery ? undefined : true })}
        />
        <Chip
          label="Failed only"
          tone="err"
          on={query.isSuccessful === false}
          onClick={() => patch({ isSuccessful: query.isSuccessful === false ? undefined : false })}
        />
      </div>

      {error ? (
        <ErrorState message={error} onRetry={reload} />
      ) : loading && !data ? (
        <div className="table-wrap"><Skeleton /></div>
      ) : items.length === 0 ? (
        <EmptyState
          title="No SQL queries captured"
          hint={hasFilters
            ? 'Nothing matches the current filters.'
            : 'Attach the EF Core interceptor when registering your DbContext:'}
          snippet={hasFilters ? undefined : SETUP}
        />
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Query</th>
                <SortHeader label="Duration" field="executiontimems" query={query} onSort={toggleSort} className="num fit" />
                <th className="num fit">Rows</th>
                <SortHeader label="When" field="timestamp" query={query} onSort={toggleSort} className="fit" />
              </tr>
            </thead>
            <tbody>
              {items.map((q, i) => (
                <tr
                  key={q.id}
                  className={i === sel ? 'sel' : ''}
                  onClick={() => { setSel(i); onOpen({ kind: 'query', id: q.id }); }}
                >
                  <td className="primary" style={{ maxWidth: 600 }} title={q.query}>
                    {!q.isSuccessful && <span className="status s5" style={{ marginRight: 6 }} />}
                    {q.query}
                  </td>
                  <td className="num fit">
                    <Duration ms={q.executionTimeMs} max={maxMs} slow={q.isSlowQuery} />
                  </td>
                  <td className="num dim fit">{q.rowsAffected > 0 ? q.rowsAffected : ''}</td>
                  <td className="dim fit"><RelTime iso={q.timestamp} tick={tick} /></td>
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
