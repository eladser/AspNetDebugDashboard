import { useMemo } from 'react';
import { api } from '../api';
import { Chip, Duration, EmptyState, ErrorState, FootBar, MethodTag, RelTime, SearchBox, Skeleton, SortHeader, StatusCode } from '../ui';
import { useList, useRowNav } from '../useList';
import type { DetailRef } from './Detail';

const SETUP = `// on by default once the dashboard is installed
config.CaptureOutgoingHttp = true;`;

export function OutgoingHttp({
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
  const { query, patch, toggleSort, setPage, data, error, loading, reload } = useList(api.outgoingHttp, tick);
  const items = data?.items ?? [];
  const { sel, setSel } = useRowNav(items, (r) => onOpen({ kind: 'http', id: r.id }), navEnabled);
  const maxMs = useMemo(() => Math.max(...items.map((r) => r.executionTimeMs), 1), [items]);

  const hasFilters = !!(query.search || query.host || query.isSuccessful === false);

  return (
    <>
      <div className="filters">
        <SearchBox
          inputRef={searchRef}
          value={query.search ?? ''}
          onChange={(search) => patch({ search: search || undefined })}
          placeholder="Filter by URL…"
        />
        <SearchBox
          value={query.host ?? ''}
          onChange={(host) => patch({ host: host || undefined })}
          placeholder="Filter by host…"
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
          title="No outgoing calls captured"
          hint={hasFilters
            ? 'Nothing matches the current filters.'
            : 'Calls your app makes with HttpClient show up here (new HttpClient(), IHttpClientFactory, SDK clients built on top of it). If this is a fresh install:'}
          snippet={hasFilters ? undefined : SETUP}
        />
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th className="fit">Method</th>
                <th>Url</th>
                <SortHeader label="Status" field="statuscode" query={query} onSort={toggleSort} className="fit" />
                <SortHeader label="Duration" field="executiontimems" query={query} onSort={toggleSort} className="num fit" />
                <SortHeader label="When" field="timestamp" query={query} onSort={toggleSort} className="fit" />
              </tr>
            </thead>
            <tbody>
              {items.map((r, i) => (
                <tr
                  key={r.id}
                  className={i === sel ? 'sel' : ''}
                  onClick={() => { setSel(i); onOpen({ kind: 'http', id: r.id }); }}
                >
                  <td className="fit"><MethodTag method={r.method} /></td>
                  <td className="primary" title={r.url}>{r.host}{new URL(r.url, 'http://x').pathname}</td>
                  <td className="fit">
                    {r.statusCode ? <StatusCode code={r.statusCode} /> : <span className="level error">ERR</span>}
                  </td>
                  <td className="num fit">
                    <Duration ms={r.executionTimeMs} max={maxMs} />
                  </td>
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
