import { useCallback, useEffect, useRef, useState } from 'react';
import type { ListQuery, PagedResult } from './types';

type Fetcher<T> = (q: ListQuery, signal?: AbortSignal) => Promise<PagedResult<T>>;

// Shared list state for the entry tables: paging, search, filters, sorting,
// and an auto-refresh tick driven by the parent.
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

  // click the active column again to flip direction
  const toggleSort = useCallback((field: string) => {
    setQuery((q) => ({
      ...q,
      page: 1,
      sortBy: field,
      sortDescending: q.sortBy === field ? !q.sortDescending : true,
    }));
  }, []);

  return {
    query,
    patch,
    toggleSort,
    setPage: (page: number) => setQuery((q) => ({ ...q, page })),
    data,
    error,
    loading,
    reload: () => load(false),
  };
}

// j/k/Enter row navigation for list views. Inactive while typing in an input
// or when the detail panel is open.
export function useRowNav<T>(items: T[], onOpen: (item: T) => void, enabled: boolean) {
  const [sel, setSel] = useState(-1);

  useEffect(() => setSel(-1), [items]);

  useEffect(() => {
    if (!enabled) return;
    const onKey = (e: KeyboardEvent) => {
      const tag = (e.target as HTMLElement).tagName;
      if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') return;
      if (e.key === 'j' || e.key === 'ArrowDown') {
        setSel((s) => Math.min(items.length - 1, s + 1));
        e.preventDefault();
      } else if (e.key === 'k' || e.key === 'ArrowUp') {
        setSel((s) => Math.max(0, s - 1));
        e.preventDefault();
      } else if (e.key === 'Enter') {
        setSel((s) => {
          if (s >= 0 && s < items.length) onOpen(items[s]);
          return s;
        });
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [items, onOpen, enabled]);

  return { sel, setSel };
}
