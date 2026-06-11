import { useEffect, useRef, useState } from 'react';
import type { ListQuery, PagedResult } from './types';
import { fmtDuration, fmtDateTime, timeAgo } from './format';

export function MethodTag({ method }: { method: string }) {
  const m = method.toLowerCase();
  const known = ['get', 'post', 'put', 'patch', 'delete'];
  return <span className={`tag ${known.includes(m) ? m : 'other'}`}>{method.toUpperCase()}</span>;
}

export function StatusCode({ code }: { code: number }) {
  return <span className={`status s${Math.floor(code / 100)}`}>{code}</span>;
}

export function LevelTag({ level }: { level: string }) {
  return <span className={`level ${level.toLowerCase()}`}>{level.toUpperCase()}</span>;
}

// Duration with a proportional bar. `max` is the largest value in the current
// view so bars are comparable within a page.
export function Duration({ ms, max, slow }: { ms: number; max: number; slow?: boolean }) {
  const pct = max > 0 ? Math.max(3, (ms / max) * 100) : 0;
  return (
    <span className={`dur${slow ? ' slow' : ''}`}>
      {fmtDuration(ms)}
      <span className="bar"><i style={{ width: `${pct}%` }} /></span>
    </span>
  );
}

// Relative timestamp that re-renders on a shared 10s tick; exact time on hover.
export function RelTime({ iso, tick }: { iso: string; tick?: number }) {
  void tick;
  return <span title={fmtDateTime(iso)}>{timeAgo(iso)}</span>;
}

export function SortHeader({
  label,
  field,
  query,
  onSort,
  className,
}: {
  label: string;
  field: string;
  query: ListQuery;
  onSort: (field: string) => void;
  className?: string;
}) {
  const active = query.sortBy === field;
  return (
    <th className={`sortable${className ? ` ${className}` : ''}`} onClick={() => onSort(field)}>
      {label}
      {active && <span className="arrow">{query.sortDescending ? '▼' : '▲'}</span>}
    </th>
  );
}

export function Chip({
  label,
  on,
  onClick,
  tone,
}: {
  label: string;
  on: boolean;
  onClick: () => void;
  tone?: 'warn' | 'err';
}) {
  return (
    <button className={`chip${on ? ' on' : ''}${tone ? ` ${tone}-chip` : ''}`} onClick={onClick}>
      {label}
    </button>
  );
}

export function CopyButton({ text, label = 'copy' }: { text: string; label?: string }) {
  const [done, setDone] = useState(false);
  return (
    <button
      className={`copy-btn${done ? ' done' : ''}`}
      onClick={(e) => {
        e.stopPropagation();
        navigator.clipboard.writeText(text).then(() => {
          setDone(true);
          setTimeout(() => setDone(false), 1500);
        });
      }}
    >
      {done ? 'copied' : label}
    </button>
  );
}

export function CodeBlock({ text, children }: { text: string; children?: React.ReactNode }) {
  return (
    <div className="codeblock">
      <pre className="code">{children ?? text}</pre>
      <CopyButton text={text} />
    </div>
  );
}

export function Skeleton({ rows = 8 }: { rows?: number }) {
  return (
    <div className="skeleton-rows">
      {Array.from({ length: rows }, (_, i) => (
        <div key={i} className="skeleton" style={{ width: `${88 - (i % 4) * 9}%` }} />
      ))}
    </div>
  );
}

export function EmptyState({
  title,
  hint,
  snippet,
}: {
  title: string;
  hint?: React.ReactNode;
  snippet?: string;
}) {
  return (
    <div className="state-box">
      <div className="title">{title}</div>
      {hint && <div className="hint">{hint}</div>}
      {snippet && <pre className="snippet">{snippet}</pre>}
    </div>
  );
}

export function ErrorState({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <div className="state-box error">
      <div className="title">Couldn't load data</div>
      <div>{message}</div>
      <button className="btn" onClick={onRetry}>Retry</button>
    </div>
  );
}

export function Pager({
  result,
  onPage,
}: {
  result: PagedResult<unknown>;
  onPage: (p: number) => void;
}) {
  if (result.totalPages <= 1) return null;
  return (
    <span className="pager">
      <button disabled={!result.hasPreviousPage} onClick={() => onPage(result.page - 1)}>prev</button>
      <span>{result.page} / {result.totalPages}</span>
      <button disabled={!result.hasNextPage} onClick={() => onPage(result.page + 1)}>next</button>
    </span>
  );
}

export function FootBar({
  result,
  onPage,
}: {
  result: PagedResult<unknown> | null;
  onPage: (p: number) => void;
}) {
  return (
    <div className="foot">
      {result && <Pager result={result} onPage={onPage} />}
      {result && <span>{result.totalCount.toLocaleString()} entries</span>}
      <span className="hints">
        <span><kbd>j</kbd><kbd>k</kbd> rows</span>
        <span><kbd>enter</kbd> open</span>
        <span><kbd>/</kbd> filter</span>
        <span><kbd>esc</kbd> close</span>
      </span>
    </div>
  );
}

// Debounced search input — typing stays snappy, queries fire 300ms after the
// last keystroke. Exposes its input via inputRef for the `/` shortcut.
export function SearchBox({
  value,
  onChange,
  placeholder,
  inputRef,
}: {
  value: string;
  onChange: (v: string) => void;
  placeholder: string;
  inputRef?: React.RefObject<HTMLInputElement>;
}) {
  const [text, setText] = useState(value);
  const timer = useRef<number>();

  useEffect(() => setText(value), [value]);

  return (
    <input
      ref={inputRef}
      className="search"
      value={text}
      placeholder={placeholder}
      onChange={(e) => {
        const v = e.target.value;
        setText(v);
        window.clearTimeout(timer.current);
        timer.current = window.setTimeout(() => onChange(v), 300);
      }}
      onKeyDown={(e) => {
        if (e.key === 'Escape') (e.target as HTMLInputElement).blur();
        e.stopPropagation();
      }}
    />
  );
}
