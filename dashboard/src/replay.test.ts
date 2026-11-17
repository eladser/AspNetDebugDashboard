import { describe, expect, it } from 'vitest';
import { buildDraft, compareRows, countRedacted, encodePath, parseHeaderLines, replayUrl, toFetchInit, type ReplayDraft } from './replay';
import type { RequestEntry } from './types';

const ORIGIN = 'http://localhost:5000';

function entry(over: Partial<RequestEntry> = {}): RequestEntry {
  return {
    id: '3f2b8c1e-0d7a-4e55-9a51-2f4d1c0b9e77',
    requestId: '0HN1',
    timestamp: '2026-11-17T10:00:00Z',
    type: 'request',
    metadata: {},
    method: 'POST',
    url: `${ORIGIN}/api/orders?expand=lines`,
    path: '/api/orders',
    queryString: '?expand=lines',
    statusCode: 201,
    executionTimeMs: 120,
    headers: { 'Content-Type': 'application/json', Accept: '*/*' },
    requestBody: '{"sku":"A-100","qty":2}',
    contentType: 'application/json',
    sqlQueries: [],
    logs: [],
    dumps: [],
    outgoingHttp: [],
    requestSize: 23,
    responseSize: 0,
    isHttps: false,
    ...over,
  };
}

function draft(over: Partial<ReplayDraft> = {}): ReplayDraft {
  return { ...buildDraft(entry(), ORIGIN), ...over };
}

describe('parseHeaderLines', () => {
  it('drops forbidden names and sec-/proxy- prefixes', () => {
    const r = parseHeaderLines([
      'Host: localhost:5000',
      'Content-Length: 23',
      'Connection: keep-alive',
      'Accept-Encoding: gzip, br',
      'Cookie: a=1',
      'Origin: http://localhost:5000',
      'Referer: http://localhost:5000/',
      'sec-ch-ua: "Chromium";v="142"',
      'Sec-Fetch-Mode: cors',
      'Proxy-Connection: keep-alive',
      'Accept: application/json',
      'X-Tenant: acme',
    ].join('\n'));

    expect(r.headers).toEqual([['Accept', 'application/json'], ['X-Tenant', 'acme']]);
    expect(r.dropped).toEqual([
      'Host', 'Content-Length', 'Connection', 'Accept-Encoding', 'Cookie', 'Origin', 'Referer',
      'sec-ch-ua', 'Sec-Fetch-Mode', 'Proxy-Connection',
    ]);
    expect(r.errors).toEqual([]);
  });

  it('drops method override headers only when they name a forbidden method', () => {
    const r = parseHeaderLines('X-HTTP-Method-Override: TRACE\nX-Method-Override: PATCH');
    expect(r.headers).toEqual([['X-Method-Override', 'PATCH']]);
    expect(r.dropped).toEqual(['X-HTTP-Method-Override']);
  });

  it('reports invalid names and values instead of passing them on', () => {
    const r = parseHeaderLines('Bad Name: x\nX-Ok: fine\nX-Emoji: ok ✓\nno colon here\n: empty');
    expect(r.headers).toEqual([['X-Ok', 'fine']]);
    expect(r.errors).toHaveLength(4);
    expect(r.errors[0]).toContain('Bad Name');
    expect(r.errors[1]).toContain('X-Emoji');
    expect(r.errors[2]).toContain('line 4');
    expect(r.errors[3]).toContain('line 5');
  });

  it('trims values, skips blank lines and keeps colons inside values', () => {
    const r = parseHeaderLines('\n  X-Url:   http://a/b:c  \n\n');
    expect(r.headers).toEqual([['X-Url', 'http://a/b:c']]);
    expect(r.errors).toEqual([]);
  });

  it('every header it keeps is accepted by Headers', () => {
    const r = parseHeaderLines('Accept: */*\nX-A: 1\nBad Name: x\nX-B: été');
    expect(() => new Headers(r.headers)).not.toThrow();
  });
});

describe('buildDraft', () => {
  it('uses the relative path and query', () => {
    const d = buildDraft(entry(), ORIGIN);
    expect(d.method).toBe('POST');
    expect(d.path).toBe('/api/orders?expand=lines');
    expect(d.originMismatch).toBeNull();
  });

  it('puts captured headers one per line, without the replay marker', () => {
    const d = buildDraft(entry({ headers: { Accept: '*/*', 'X-Debug-Replay-Of': 'abc' } }), ORIGIN);
    expect(d.headerText).toBe('Accept: */*');
  });

  it('counts *** values in the body', () => {
    const d = buildDraft(entry({ requestBody: '{"user":"bob","password":"***","apiKey":"***"}' }), ORIGIN);
    expect(d.redactedCount).toBe(2);
    expect(d.body).toContain('"password":"***"');
  });

  it('counts the antiforgery token as redacted', () => {
    const d = buildDraft(entry({
      contentType: 'application/x-www-form-urlencoded',
      requestBody: 'Email=bob%40example.com&__RequestVerificationToken=***',
    }), ORIGIN);
    expect(d.redactedCount).toBe(1);
    expect(d.bodyDisabledReason).toBeNull();
  });

  it('disables the body for multipart', () => {
    const d = buildDraft(entry({ contentType: 'multipart/form-data; boundary=----x', requestBody: '------x\r\n...' }), ORIGIN);
    expect(d.bodyDisabledReason).toMatch(/multipart/i);
    expect(d.body).toBe('');
  });

  it('disables the body for binary content types', () => {
    const d = buildDraft(entry({ contentType: 'application/octet-stream', requestBody: '\u0000\u0001' }), ORIGIN);
    expect(d.bodyDisabledReason).not.toBeNull();
  });

  it('gives a reason when the body was too large to capture', () => {
    const d = buildDraft(entry({ requestBody: '[Body too large: 2097152 bytes]', requestSize: 2097152 }), ORIGIN);
    expect(d.bodyDisabledReason).toMatch(/too large/i);
    expect(d.body).toBe('');
    expect(d.redactedCount).toBe(0);
  });

  it('gives a reason when the body exists but was not captured', () => {
    const d = buildDraft(entry({ requestBody: null, requestSize: 40 }), ORIGIN);
    expect(d.bodyDisabledReason).toMatch(/LogRequestBodies/);
  });

  it('has no reason for a request without a body', () => {
    const d = buildDraft(entry({ method: 'GET', requestBody: null, requestSize: 0, contentType: null }), ORIGIN);
    expect(d.bodyDisabledReason).toBeNull();
    expect(d.body).toBe('');
  });

  it('flags an origin that differs from the dashboard', () => {
    const d = buildDraft(entry({ url: 'https://api.example.test:8443/api/orders' }), ORIGIN);
    expect(d.originMismatch).toBe('https://api.example.test:8443');
  });

  it('copes with a null query string', () => {
    const d = buildDraft(entry({ queryString: null as unknown as string }), ORIGIN);
    expect(d.path).toBe('/api/orders');
  });
});

describe('countRedacted', () => {
  it('counts across all texts', () => {
    expect(countRedacted('a=***&b=***', 'X-Token: ***', '')).toBe(3);
    expect(countRedacted('nothing here')).toBe(0);
  });
});

describe('encodePath', () => {
  it('re-escapes characters a decoded path loses', () => {
    expect(encodePath('/a?b')).toBe('/a%3Fb');
    expect(encodePath('/a#b')).toBe('/a%23b');
    expect(encodePath('/a\\b')).toBe('/a%5Cb');
    expect(encodePath('/100%')).toBe('/100%25');
  });

  it('leaves %2F alone and plain paths unchanged', () => {
    expect(encodePath('/a%2Fb')).toBe('/a%2Fb');
    expect(encodePath('/a%2fb')).toBe('/a%2fb');
    expect(encodePath('/api/orders/7')).toBe('/api/orders/7');
  });

  it('keeps a captured backslash path on this origin', () => {
    expect(replayUrl(encodePath('/\\evil.com/x'), ORIGIN)).toBe(`${ORIGIN}/%5Cevil.com/x`);
  });
});

describe('replayUrl', () => {
  it('resolves normal paths against the dashboard origin', () => {
    expect(replayUrl('/api/orders?id=1', ORIGIN)).toBe(`${ORIGIN}/api/orders?id=1`);
    expect(replayUrl('api/orders', ORIGIN)).toBe(`${ORIGIN}/api/orders`);
    expect(replayUrl(`${ORIGIN}/api/orders#top`, ORIGIN)).toBe(`${ORIGIN}/api/orders`);
  });

  it('rejects anything that leaves the origin', () => {
    expect(replayUrl('/\\evil.com/x', ORIGIN)).toBeNull();
    expect(replayUrl('\\\\evil.com', ORIGIN)).toBeNull();
    expect(replayUrl('//evil.com/x', ORIGIN)).toBeNull();
    expect(replayUrl('https://evil.com/x', ORIGIN)).toBeNull();
    expect(replayUrl('http://localhost:5001/x', ORIGIN)).toBeNull();
  });

  it('rejects tabs, newlines and other control characters', () => {
    expect(replayUrl('/api/\torders', ORIGIN)).toBeNull();
    expect(replayUrl('/api/orders\n', ORIGIN)).toBeNull();
    expect(replayUrl('/\n/evil.com/x', ORIGIN)).toBeNull();
    expect(replayUrl('/api/\u007forders', ORIGIN)).toBeNull();
  });

  it('keeps an encoded backslash as a literal path on the same origin', () => {
    expect(replayUrl('/%5Cevil.com', ORIGIN)).toBe(`${ORIGIN}/%5Cevil.com`);
  });

  it('returns an absolute URL, so a path that normalizes to // stays on the origin', () => {
    const url = replayUrl('/.//evil.com/x', ORIGIN)!;
    expect(new URL(url).origin).toBe(ORIGIN);
    expect(new URL(url, 'https://elsewhere.test').origin).toBe(ORIGIN);
  });
});

describe('toFetchInit', () => {
  const id = '3f2b8c1e-0d7a-4e55-9a51-2f4d1c0b9e77';

  it('never sends a body with GET or HEAD', () => {
    expect(toFetchInit(draft({ method: 'GET', body: '{"a":1}' }), id).body).toBeUndefined();
    expect(toFetchInit(draft({ method: 'head', body: '{"a":1}' }), id).body).toBeUndefined();
  });

  it('sends the body for other methods', () => {
    const init = toFetchInit(draft(), id);
    expect(init.method).toBe('POST');
    expect(init.body).toBe('{"sku":"A-100","qty":2}');
  });

  it('skips the body when it is disabled', () => {
    const init = toFetchInit(draft({ body: 'x', bodyDisabledReason: 'Multipart' }), id);
    expect(init.body).toBeUndefined();
  });

  it('sends same-origin credentials, manual redirects and the replay marker', () => {
    const init = toFetchInit(draft({ headerText: 'Host: x\nX-Debug-Replay-Of: old\nX-A: 1' }), id);
    expect(init.credentials).toBe('same-origin');
    expect(init.redirect).toBe('manual');
    const h = init.headers as Headers;
    expect(h.get('X-Debug-Replay-Of')).toBe(id);
    expect(h.get('X-A')).toBe('1');
    expect(h.has('Host')).toBe(false);
  });
});

describe('compareRows', () => {
  it('colors faster and fewer as better, slower and more as worse', () => {
    const before = entry({ executionTimeMs: 400, sqlQueries: Array(12).fill({}) });
    const after = entry({ executionTimeMs: 60, sqlQueries: Array(2).fill({}), outgoingHttp: [{} as never] });
    const rows = compareRows(before, after);

    expect(rows.map((r) => r.label)).toEqual(['Status', 'Duration', 'SQL queries', 'Outgoing calls']);
    expect(rows[1]).toMatchObject({ delta: '-340 ms', tone: 'better' });
    expect(rows[2]).toMatchObject({ delta: '-10', tone: 'better' });
    expect(rows[3]).toMatchObject({ delta: '+1', tone: 'worse' });
  });

  it('uses durationMs when an entry has it, executionTimeMs otherwise', () => {
    const rows = compareRows(entry({ executionTimeMs: 9, durationMs: 9.9 }), entry({ executionTimeMs: 2, durationMs: 2.1 }));
    expect(rows[1]).toMatchObject({ before: 9.9, after: 2.1, delta: '-8 ms', tone: 'better' });

    const old = compareRows(entry({ executionTimeMs: 400 }), entry({ executionTimeMs: 60, durationMs: 60.4 }));
    expect(old[1]).toMatchObject({ before: 400, after: 60.4, delta: '-340 ms' });
  });

  it('treats tiny duration changes as noise', () => {
    const rows = compareRows(entry({ executionTimeMs: 40 }), entry({ executionTimeMs: 43 }));
    expect(rows[1].tone).toBe('same');
  });

  it('marks a changed status', () => {
    const rows = compareRows(entry({ statusCode: 200 }), entry({ statusCode: 400 }));
    expect(rows[0]).toMatchObject({ delta: 'changed', tone: 'worse' });
    expect(compareRows(entry(), entry())[0].tone).toBe('same');
  });
});
