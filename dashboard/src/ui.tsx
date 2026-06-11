import { useEffect, useRef, useState } from 'react';
import type { PagedResult } from './types';

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

export function Skeleton({ rows = 8 }: { rows?: number }) {
  return (
    <div className="skeleton-rows">
      {Array.from({ length: rows }, (_, i) => (
        <div key={i} className="skeleton" style={{ width: `${88 - (i % 4) * 9}%` }} />
      ))}
    </div>
  );
}

export function EmptyState({ title, hint }: { title: string; hint?: React.ReactNode }) {
  return (
    <div className="state-box">
      <div className="title">{title}</div>
      {hint && <div>{hint}</div>}
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
    <div className="pager">
      <button disabled={!result.hasPreviousPage} onClick={() => onPage(result.page - 1)}>
        prev
      </button>
      <span>
        {result.page} / {result.totalPages}
      </span>
      <button disabled={!result.hasNextPage} onClick={() => onPage(result.page + 1)}>
        next
      </button>
    </div>
  );
}

// Debounced search input — keeps typing snappy, queries fire 300ms after the
// last keystroke.
export function SearchBox({
  value,
  onChange,
  placeholder,
}: {
  value: string;
  onChange: (v: string) => void;
  placeholder: string;
}) {
  const [text, setText] = useState(value);
  const timer = useRef<number>();

  useEffect(() => setText(value), [value]);

  return (
    <input
      className="search"
      value={text}
      placeholder={placeholder}
      onChange={(e) => {
        const v = e.target.value;
        setText(v);
        window.clearTimeout(timer.current);
        timer.current = window.setTimeout(() => onChange(v), 300);
      }}
    />
  );
}
