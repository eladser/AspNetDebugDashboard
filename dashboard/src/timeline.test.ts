import { describe, expect, it } from 'vitest';
import { breakdown, buildRows, clampPct, fmtMs, hasTimeline, labelSide, nPlusOneGroups, tickValues, unionMs } from './timeline';
import type { RequestEntry } from './types';

let n = 0;
const base = () => ({ id: `e${++n}`, requestId: 'r', timestamp: '', type: '', metadata: {} });

const sql = (query: string, off: number | null, dur: number, slow = false) =>
  ({ ...base(), query, parameters: {}, executionTimeMs: Math.round(dur), durationMs: dur, rowsAffected: 0,
     isSuccessful: true, isSlowQuery: slow, startOffsetMs: off }) as RequestEntry['sqlQueries'][number];

const http = (off: number | null, dur: number) =>
  ({ ...base(), method: 'GET', url: 'https://api.test/x', host: 'api.test', statusCode: 200,
     executionTimeMs: Math.round(dur), durationMs: dur, requestHeaders: {}, responseHeaders: {},
     startOffsetMs: off }) as RequestEntry['outgoingHttp'][number];

const log = (off: number | null | undefined) =>
  ({ ...base(), level: 'Information', message: 'hi', properties: {}, startOffsetMs: off }) as RequestEntry['logs'][number];

const req = (p: Partial<RequestEntry> = {}) =>
  ({ ...base(), method: 'GET', url: '', path: '/x', queryString: '', statusCode: 200, executionTimeMs: 160,
     durationMs: 160, headers: {}, sqlQueries: [], logs: [], dumps: [], outgoingHttp: [], requestSize: 0,
     responseSize: 0, isHttps: false, ...p }) as RequestEntry;

describe('unionMs', () => {
  it('is 0 for no intervals', () => expect(unionMs([])).toBe(0));
  it('adds disjoint intervals', () => expect(unionMs([[0, 10], [20, 25]])).toBe(15));
  it('merges overlapping intervals', () => expect(unionMs([[0, 10], [5, 20]])).toBe(20));
  it('counts nested intervals once', () => expect(unionMs([[0, 50], [10, 20], [30, 40]])).toBe(50));
  it('handles unsorted input', () => expect(unionMs([[20, 30], [0, 10], [8, 22]])).toBe(30));
});

describe('breakdown', () => {
  it('computes app time from the union of all lanes', () => {
    const r = req({ sqlQueries: [sql('a', 0, 20), sql('b', 30, 21)], outgoingHttp: [http(60, 60)] });
    expect(breakdown(r)).toEqual({ sqlMs: 41, httpMs: 60, appMs: 59, totalMs: 160, parallel: false });
  });

  it('flags parallel when sql and http overlap', () => {
    const r = req({ sqlQueries: [sql('a', 10, 40)], outgoingHttp: [http(20, 100)] });
    const b = breakdown(r);
    expect(b.parallel).toBe(true);
    expect(b.appMs).toBe(50);
  });

  it('counts overlap inside a lane once', () => {
    const r = req({ outgoingHttp: [http(0, 50), http(10, 20)] });
    expect(breakdown(r).httpMs).toBe(50);
  });

  it('falls back to executionTimeMs and clamps past the end', () => {
    const r = req({ durationMs: null, executionTimeMs: 100, sqlQueries: [sql('a', 90, 50)] });
    const b = breakdown(r);
    expect(b.totalMs).toBe(100);
    expect(b.sqlMs).toBe(10);
    expect(b.appMs).toBe(90);
  });

  it('ignores children without offsets', () => {
    const r = req({ sqlQueries: [sql('a', null, 50)] });
    expect(breakdown(r).sqlMs).toBe(0);
  });
});

describe('clampPct', () => {
  it('maps offset and duration to percentages', () => {
    expect(clampPct(40, 20, 200)).toEqual({ left: 20, width: 10 });
  });
  it('clamps below 0', () => {
    expect(clampPct(-10, 30, 100)).toEqual({ left: 0, width: 20 });
  });
  it('clamps past the total', () => {
    expect(clampPct(90, 50, 100)).toEqual({ left: 90, width: 10 });
    expect(clampPct(150, 5, 100)).toEqual({ left: 100, width: 0 });
  });
  it('survives a zero total', () => {
    expect(clampPct(5, 5, 0)).toEqual({ left: 0, width: 0 });
  });
});

describe('nPlusOneGroups', () => {
  it('returns every group of 3+, largest first', () => {
    const qs = [
      ...Array.from({ length: 3 }, (_, i) => sql('SELECT a', i, 1)),
      ...Array.from({ length: 5 }, (_, i) => sql('SELECT b', 10 + i, 1)),
      sql('SELECT c', 20, 1),
      sql('SELECT c', 21, 1),
    ];
    expect(nPlusOneGroups(qs).map((x) => [x.query, x.count])).toEqual([['SELECT b', 5], ['SELECT a', 3]]);
  });
  it('is empty below 3', () => {
    expect(nPlusOneGroups([sql('x', 0, 1), sql('x', 1, 1)])).toEqual([]);
  });
});

describe('hasTimeline', () => {
  it('is false without offsets', () => {
    expect(hasTimeline(req({ sqlQueries: [sql('a', null, 5)], logs: [log(undefined)] }))).toBe(false);
    expect(hasTimeline(req())).toBe(false);
  });
  it('is true when any child has an offset', () => {
    expect(hasTimeline(req({ logs: [log(12)] }))).toBe(true);
  });
  it('is false when the total is unknown', () => {
    expect(hasTimeline(req({ durationMs: null, executionTimeMs: 0, logs: [log(1)] }))).toBe(false);
  });
});

describe('buildRows', () => {
  it('orders rows by offset and skips children without one', () => {
    const r = req({ sqlQueries: [sql('late', 50, 5), sql('none', null, 5)], logs: [log(10)], outgoingHttp: [http(30, 10)] });
    expect(buildRows(r).map((x) => x.lane)).toEqual(['log', 'http', 'sql']);
  });

  it('puts the exception marker at the request end', () => {
    const r = req({ exception: 'System.InvalidOperationException: boom\n at X', logs: [log(5)] });
    const rows = buildRows(r);
    const ex = rows[rows.length - 1];
    expect(ex.lane).toBe('exception');
    expect(ex.offset).toBe(160);
    expect(ex.title).toContain('System.InvalidOperationException');
  });

  it('has no exception row without an exception', () => {
    expect(buildRows(req({ logs: [log(5)] })).some((x) => x.lane === 'exception')).toBe(false);
  });

  it('marks slow rows and tags the first row of each N+1 group', () => {
    const qs = [sql('q', 5, 2), sql('q', 10, 2), sql('q', 15, 2), sql('slow', 20, 90, true), sql('fast', 30, 1)];
    const rows = buildRows(req({ sqlQueries: qs }));
    expect(rows.map((x) => x.warn)).toEqual([true, true, true, true, false]);
    expect(rows.map((x) => x.nPlusOne)).toEqual([3, undefined, undefined, undefined, undefined]);
  });

  it('truncates long sql in the tooltip', () => {
    const rows = buildRows(req({ sqlQueries: [sql('x'.repeat(500), 1, 1)] }));
    expect(rows[0].title.length).toBeLessThan(260);
  });
});

describe('tickValues', () => {
  it('gives 4 to 6 ticks starting at 0', () => {
    for (const t of [3, 7, 16, 160, 333, 1200, 4800, 98765]) {
      const v = tickValues(t);
      expect(v.length).toBeGreaterThanOrEqual(4);
      expect(v.length).toBeLessThanOrEqual(6);
      expect(v[0]).toBe(0);
      expect(v[v.length - 1]).toBeLessThanOrEqual(t);
    }
  });
});

describe('fmtMs', () => {
  it('keeps sub-ms ticks apart', () => {
    expect(tickValues(0.9).map(fmtMs)).toEqual(['0 ms', '0.2 ms', '0.4 ms', '0.6 ms', '0.8 ms']);
    expect(fmtMs(1.5)).toBe('1.5 ms');
  });
  it('rounds like fmtDuration from 10 ms up', () => {
    expect(fmtMs(314.4)).toBe('314 ms');
    expect(fmtMs(2500)).toBe('2.50 s');
  });
});

describe('labelSide', () => {
  it('picks the roomier side', () => {
    expect(labelSide(2, 1)).toBe('right');
    expect(labelSide(80, 5)).toBe('left');
    expect(labelSide(0, 40)).toBe('right');
    expect(labelSide(30, 35)).toBe('right');
  });
  it('goes inside a bar wider than the space on either side', () => {
    expect(labelSide(3, 97)).toBe('in');
    expect(labelSide(23, 77)).toBe('in');
    expect(labelSide(0, 60)).toBe('in');
  });
});
