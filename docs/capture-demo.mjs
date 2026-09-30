// Records docs/images/demo.gif and the dashboard screenshots from a running SampleApp.
// No npm deps: Node 22+ (global fetch + WebSocket), Chrome, and ffmpeg on PATH.
//
//   dotnet run --project samples/SampleApp --urls http://localhost:5000
//   node docs/capture-demo.mjs [--url http://localhost:5000] [--out docs/images] [--keep-frames]
//
// Chrome is driven headless over CDP. Frames come from Page.startScreencast (JPEG) and are
// stitched with ffmpeg palettegen/paletteuse using each frame's real timestamp, so the gif
// plays back at the speed it was recorded. A fake cursor is drawn in the page because
// headless Chrome doesn't render one.

import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

const args = process.argv.slice(2);
const opt = (name, def) => {
  const i = args.indexOf(`--${name}`);
  return i >= 0 ? args[i + 1] : def;
};
const BASE = opt('url', 'http://localhost:5000').replace(/\/$/, '');
const OUT = resolve(opt('out', join(import.meta.dirname, 'images')));
const KEEP = args.includes('--keep-frames');
const CHROME = process.env.CHROME ?? (process.platform === 'win32'
  ? 'C:/Program Files/Google/Chrome/Application/chrome.exe'
  : process.platform === 'darwin'
    ? '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'
    : 'google-chrome');
const PORT = 9333;
const W = 1440, H = 900;          // screenshot size
const GIF_W = 960, GIF_FPS = 18;  // gif is scaled down from the same frames
const SPEED = 1.3;                 // playback speed-up, keeps the gif near 30s

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const tmp = mkdtempSync(join(tmpdir(), 'debug-demo-'));
const framesDir = join(tmp, 'frames');
mkdirSync(framesDir);

// --- chrome + cdp ---------------------------------------------------------

const chrome = spawn(CHROME, [
  '--headless=new', `--remote-debugging-port=${PORT}`, `--user-data-dir=${join(tmp, 'profile')}`,
  `--window-size=${W},${H}`, '--hide-scrollbars', '--no-first-run', '--no-default-browser-check',
  '--force-device-scale-factor=1', 'about:blank',
], { stdio: 'ignore' });

let wsUrl;
for (let i = 0; i < 50 && !wsUrl; i++) {
  try {
    const list = await (await fetch(`http://127.0.0.1:${PORT}/json/list`)).json();
    wsUrl = list.find((t) => t.type === 'page')?.webSocketDebuggerUrl;
  } catch { /* not up yet */ }
  if (!wsUrl) await sleep(200);
}
if (!wsUrl) throw new Error(`Chrome didn't expose a page on port ${PORT}`);

const ws = new WebSocket(wsUrl);
await new Promise((r, j) => { ws.onopen = r; ws.onerror = j; });
let seq = 0;
const pending = new Map();
const handlers = new Map();
ws.onmessage = (m) => {
  const msg = JSON.parse(m.data);
  if (msg.id && pending.has(msg.id)) {
    const { res, rej } = pending.get(msg.id);
    pending.delete(msg.id);
    msg.error ? rej(new Error(`${msg.error.message} (${msg.error.code})`)) : res(msg.result);
  } else if (msg.method) {
    handlers.get(msg.method)?.(msg.params);
  }
};
const cdp = (method, params = {}) => new Promise((res, rej) => {
  const id = ++seq;
  pending.set(id, { res, rej });
  ws.send(JSON.stringify({ id, method, params }));
});
const js = async (expr) => {
  const r = await cdp('Runtime.evaluate', { expression: expr, awaitPromise: true, returnByValue: true });
  if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description ?? expr);
  return r.result.value;
};

// --- cursor + input -------------------------------------------------------

const CURSOR = `
(() => {
  const add = () => {
    if (document.getElementById('__cursor')) return;
    const c = document.createElement('div');
    c.id = '__cursor';
    c.innerHTML = '<svg width="22" height="22" viewBox="0 0 24 24"><path d="M4 2l15 11-6.5 1.2L16 21l-3 1.3-3.4-6.8L4 19z" fill="#fff" stroke="#111" stroke-width="1.4" stroke-linejoin="round"/></svg>';
    Object.assign(c.style, { position: 'fixed', left: '0', top: '0', zIndex: 2147483647, pointerEvents: 'none',
      transform: 'translate(-100px,-100px)', filter: 'drop-shadow(0 1px 2px rgba(0,0,0,.5))' });
    document.documentElement.appendChild(c);
    addEventListener('mousemove', (e) => { c.style.transform = 'translate(' + (e.clientX - 3) + 'px,' + (e.clientY - 2) + 'px)'; }, true);
    addEventListener('mousedown', () => { c.style.scale = '0.85'; }, true);
    addEventListener('mouseup', () => { c.style.scale = '1'; }, true);
  };
  document.readyState === 'loading' ? addEventListener('DOMContentLoaded', add) : add();
})();`;

let mouse = { x: W * 0.6, y: H * 0.55 };

async function moveTo(x, y, ms = 450) {
  const steps = Math.max(8, Math.round(ms / 16));
  const from = { ...mouse };
  for (let i = 1; i <= steps; i++) {
    const t = i / steps;
    const e = t < 0.5 ? 2 * t * t : 1 - (-2 * t + 2) ** 2 / 2;
    mouse = { x: from.x + (x - from.x) * e, y: from.y + (y - from.y) * e };
    await cdp('Input.dispatchMouseEvent', { type: 'mouseMoved', x: mouse.x, y: mouse.y });
    await sleep(ms / steps);
  }
}

async function click() {
  const p = { x: mouse.x, y: mouse.y, button: 'left', clickCount: 1 };
  await cdp('Input.dispatchMouseEvent', { type: 'mousePressed', ...p });
  await sleep(70);
  await cdp('Input.dispatchMouseEvent', { type: 'mouseReleased', ...p });
}

// Center of the first element matching `sel` whose text contains `text`, after
// scrolling it into view. Waits up to `timeout` for it to show up.
async function find(sel, text = '', timeout = 8000) {
  const end = Date.now() + timeout;
  while (Date.now() < end) {
    const box = await js(`(() => {
      const el = [...document.querySelectorAll(${JSON.stringify(sel)})]
        .find((e) => e.offsetParent !== null && e.textContent.includes(${JSON.stringify(text)}));
      if (!el) return null;
      el.scrollIntoView({ block: 'nearest' });
      const r = el.getBoundingClientRect();
      return { x: r.left + Math.min(r.width / 2, 160), y: r.top + r.height / 2 };
    })()`);
    if (box) return box;
    await sleep(150);
  }
  throw new Error(`No element ${sel} containing "${text}"`);
}

async function clickOn(sel, text, { pause = 250, ms } = {}) {
  const { x, y } = await find(sel, text);
  await moveTo(x, y, ms);
  await sleep(pause);
  await click();
}

async function press(key, code, keyCode) {
  await cdp('Input.dispatchKeyEvent', { type: 'keyDown', key, code, windowsVirtualKeyCode: keyCode });
  await cdp('Input.dispatchKeyEvent', { type: 'keyUp', key, code, windowsVirtualKeyCode: keyCode });
}
const esc = () => press('Escape', 'Escape', 27);

async function screenshot(name) {
  await js(`document.getElementById('__cursor')?.style.setProperty('visibility', 'hidden')`);
  const { data } = await cdp('Page.captureScreenshot', { format: 'png' });
  writeFileSync(join(OUT, name), Buffer.from(data, 'base64'));
  await js(`document.getElementById('__cursor')?.style.removeProperty('visibility')`);
  console.log(`wrote ${name}`);
}

// --- traffic --------------------------------------------------------------

const hit = (path, init) => fetch(BASE + path, init).then((r) => r.text()).catch((e) => console.warn(path, e.message));
const order = (items) => hit('/api/orders', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ customerId: 2, status: 'Pending', orderItems: items }),
});

// Spaced a bit over a second apart: the UI refreshes at most once a second, so this
// makes each row land on its own.
async function traffic() {
  await hit('/api/externalapi/zen');
  await sleep(1100);
  await order([{ productId: 3, quantity: 1, unitPrice: 129.99 }, { productId: 5, quantity: 2, unitPrice: 199.99 }]);
  await sleep(1100);
  await hit('/api/products/999');
  await sleep(1100);
  await hit('/api/products/search?q=key');
}

// --- run ------------------------------------------------------------------

let ffmpegFailed = false;
try {
  await cdp('Page.enable');
  await cdp('Runtime.enable');
  await cdp('Emulation.setDeviceMetricsOverride', { width: W, height: H, deviceScaleFactor: 1, mobile: false });
  await cdp('Page.addScriptToEvaluateOnNewDocument', { source: CURSOR });

  // Start from an empty store with a little history so the lists aren't bare.
  await hit('/_debug/api/clear', { method: 'DELETE' });
  for (const p of ['/api/products', '/api/products/1', '/api/orders', '/api/products/4',
    '/api/orders/customer/1', '/api/products/slow-operation', '/api/products', '/api/orders/2']) await hit(p);

  await cdp('Page.navigate', { url: `${BASE}/_debug` });
  await find('.nav-item', 'Requests');
  await sleep(800);

  // Frames carry their capture time, which becomes each frame's duration in the gif.
  const frames = [];
  handlers.set('Page.screencastFrame', ({ data, metadata, sessionId }) => {
    const file = join(framesDir, `f${String(frames.length).padStart(5, '0')}.jpg`);
    writeFileSync(file, Buffer.from(data, 'base64'));
    frames.push({ file, t: metadata.timestamp });
    cdp('Page.screencastFrameAck', { sessionId }).catch(() => {});
  });
  await cdp('Page.startScreencast', { format: 'jpeg', quality: 92, maxWidth: W, maxHeight: H, everyNthFrame: 1 });
  await moveTo(W * 0.5, H * 0.5, 200);
  await sleep(600);

  // Requests: new rows arrive over the live stream while we watch.
  await clickOn('.nav-item', 'Requests');
  await sleep(700);
  await traffic();
  await sleep(1100);
  await screenshot('requests.png');

  // A request that called an external API: its HTTP tab.
  await clickOn('tbody tr', '/api/externalapi/zen');
  await sleep(900);
  await clickOn('.dtab', 'HTTP');
  await sleep(1100);
  await esc();
  await sleep(500);

  // The order POST: dumps captured during the request.
  await clickOn('tbody tr', 'POST');
  await sleep(900);
  await clickOn('.dtab', 'Dumps');
  await sleep(1000);
  await clickOn('.dtab', 'SQL');
  await sleep(800);
  await screenshot('request-detail.png');
  await esc();
  await sleep(500);

  // Outgoing tab and one call in detail.
  await clickOn('.nav-item', 'Outgoing');
  await sleep(700);
  await clickOn('tbody tr', 'api.github.com');
  await sleep(900);
  await clickOn('.dtab', 'Response');
  await sleep(1100);
  await esc();
  await sleep(400);

  // Dumps tab with the JSON tree, collapse and reopen a branch.
  await clickOn('.nav-item', 'Dumps');
  await sleep(900);
  await clickOn('tbody tr', 'created order');
  await sleep(1100);
  // expand the first order item (nested levels start collapsed)
  const toggle = await find('.json-tree .jv-indent .jv-indent .jv-toggle', '');
  await moveTo(toggle.x, toggle.y);
  await sleep(200);
  await click();
  await sleep(900);
  await esc();
  await sleep(400);

  // SQL
  await clickOn('.nav-item', 'Queries');
  await sleep(700);
  await clickOn('tbody tr', 'INSERT');
  await sleep(1500);
  await screenshot('query-detail.png');
  await esc();
  await sleep(400);

  await cdp('Page.stopScreencast');
  await clickOn('.nav-item', 'Overview');
  await sleep(1300);
  await screenshot('overview.png');
  await sleep(300);
  console.log(`${frames.length} frames over ${(frames.at(-1).t - frames[0].t).toFixed(1)}s`);

  // concat list: each frame shown until the next one arrived
  const lines = [];
  for (let i = 0; i < frames.length; i++) {
    const dur = i + 1 < frames.length ? frames[i + 1].t - frames[i].t : 1.5;
    lines.push(`file '${frames[i].file.replace(/\\/g, '/')}'`, `duration ${dur.toFixed(4)}`);
  }
  lines.push(`file '${frames.at(-1).file.replace(/\\/g, '/')}'`);
  const list = join(tmp, 'frames.txt');
  writeFileSync(list, lines.join('\n'));

  const gif = join(OUT, 'demo.gif');
  const vf = `setpts=PTS/${SPEED},fps=${GIF_FPS},scale=${GIF_W}:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=128:stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=5:diff_mode=rectangle`;
  const ff = spawnSync('ffmpeg', ['-y', '-loglevel', 'error', '-f', 'concat', '-safe', '0', '-i', list, '-vf', vf, '-loop', '0', gif], { stdio: 'inherit' });
  if (ff.status !== 0) { ffmpegFailed = true; throw new Error('ffmpeg failed'); }
  console.log('wrote demo.gif');
} finally {
  ws.close();
  chrome.kill();
  await sleep(500);
  if (KEEP || ffmpegFailed) console.log(`frames kept in ${tmp}`);
  else rmSync(tmp, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 });
}
