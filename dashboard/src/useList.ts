import { useCallback, useEffect, useRef, useState } from 'react';
import type { ListQuery, PagedResult } from './types';

type Fetcher<T> = (q: ListQuery, signal?: AbortSignal) => Promise<PagedResult<T>>;

// Shared list state for the four entry tables: paging, search, filters,
// and an optional auto-refresh tick driven by the parent.
export function useList<T>(fetcher: Fetcher<T>, tick: number, extra?: Partial<ListQuery>) {
  const [query, setQuery] = useState<ListQuery>({
    page: 1,
    pageSize: 50,
    sortBy: 'timestamp',
    sortDescending: true,
    ...extra,
  });
  const [data, setData] = useState<PagedResult<T> | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const abortRef = useRef<AbortController>();

  const load = useCallback(
    async (background: boolean) => {
      abortRef.current?.abort();
      const ctrl = new AbortController();
      abortRef.current = ctrl;
      if (!background) setLoading(true);
      try {
        const result = await fetcher(query, ctrl.signal);
        setData(result);
        setError(null);
      } catch (e) {
        if (!ctrl.signal.aborted) setError(e instanceof Error ? e.message : String(e));
      } finally {
        if (!ctrl.signal.aborted) setLoading(false);
      }
    },
    [fetcher, query]
  );

  useEffect(() => {
    load(false);
  }, [load]);

  // tick increments while live mode is on; refresh without flashing the skeleton
  const firstTick = useRef(true);
  useEffect(() => {
    if (firstTick.current) {
      firstTick.current = false;
      return;
    }
    load(true);
  }, [tick]); // eslint-disable-line react-hooks/exhaustive-deps

  const patch = useCallback((p: Partial<ListQuery>) => {
    setQuery((q) => ({ ...q, page: 1, ...p }));
  }, []);

  return { query, patch, setPage: (page: number) => setQuery((q) => ({ ...q, page })), data, error, loading, reload: () => load(false) };
}
