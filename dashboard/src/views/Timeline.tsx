import { useMemo, useState } from 'react';
import type { RequestEntry } from '../types';
import { breakdown, buildRows, clampPct, fmtMs, labelSide, tickValues, totalOf, type Lane } from '../timeline';
import type { DetailRef } from './Detail';

const LIMIT = 300;
const kinds: Record<Exclude<Lane, 'exception'>, 'query' | 'http' | 'log' | 'dump'> = {
  sql: 'query',
  http: 'http',
  log: 'log',
  dump: 'dump',
};

export function Timeline({ entry, onOpen }: { entry: RequestEntry; onOpen: (ref: DetailRef) => void }) {
  const [all, setAll] = useState(false);
  const rows = useMemo(() => buildRows(entry), [entry]);
  const b = useMemo(() => breakdown(entry), [entry]);
  const total = totalOf(entry);
  const ticks = tickValues(total);

  let shown = rows;
  if (!all && rows.length > LIMIT) {
    shown = rows.slice(0, LIMIT);
    const last = rows[rows.length - 1];
    if (last.lane === 'exception' && !shown.includes(last)) shown.push(last);
  }

  const parts: string[] = [];
  if (entry.sqlQueries?.length) parts.push(`SQL ${fmtMs(b.sqlMs)}`);
  if (entry.outgoingHttp?.length) parts.push(`HTTP ${fmtMs(b.httpMs)}`);
  parts.push(`app ${fmtMs(b.appMs)} of ${fmtMs(b.totalMs)}`);

  return (
    <div className="tl">
      <div
        className="tl-sum"
        title={b.parallel ? 'SQL and HTTP ran in parallel, so lane times add up to more than the request took' : undefined}
      >
        {parts.join(' · ')}
        {b.parallel && <span className="tl-par">parallel</span>}
      </div>
      <div className="tl-ruler">
        {ticks.map((t) => (
          <span key={t} className={`tl-tick${t === 0 ? ' first' : ''}`} style={{ left: `${(t * 100) / total}%` }}>
            {t === 0 ? '0' : fmtMs(t)}
          </span>
        ))}
      </div>
      <div className="tl-rows">
        <div className="tl-grid">
          {ticks.map((t) => (
            <span key={t} style={{ left: `${(t * 100) / total}%` }} />
          ))}
        </div>
        {shown.map((r) => {
          const { left, width } = clampPct(r.offset, r.dur, total);
          const after = left + width;
          const side = labelSide(left, width);
          const lane = r.lane;
          const open = lane === 'exception' ? undefined : () => onOpen({ kind: kinds[lane], id: r.id });
          return (
            <div
              key={r.id}
              className={`tl-row${open ? ' clickable' : ''}`}
              title={r.title}
              onClick={open}
            >
              <div className="tl-track">
                <span
                  className={`tl-bar ${r.lane}${r.warn ? ' warn' : ''}${r.dur === 0 ? ' tick' : ''}`}
                  style={{ left: `${left}%`, width: `${width}%` }}
                />
                <span
                  className={`tl-label${side === 'in' ? ' in' : ''}`}
                  style={side === 'in'
                    ? { left: `${left}%`, maxWidth: `${width}%`, paddingLeft: 6, paddingRight: 6 }
                    : side === 'left'
                      ? { right: `${100 - left}%`, maxWidth: `${left}%`, paddingRight: 6 }
                      : { left: `${after}%`, maxWidth: `${100 - after}%`, paddingLeft: 6 }}
                >
                  {r.nPlusOne && <b className="tl-n">×{r.nPlusOne}</b>}
                  {r.label}
                </span>
              </div>
            </div>
          );
        })}
      </div>
      {shown.length < rows.length && (
        <button className="link-btn tl-more" onClick={() => setAll(true)}>
          show all {rows.length}
        </button>
      )}
    </div>
  );
}
