import { useEffect, useState } from 'react';
import { api } from './api';

const POLL_MS = 5000;
const THROTTLE_MS = 1000;
const OPEN_TIMEOUT_MS = 5000;
const SILENCE_TIMEOUT_MS = 45000;

// Leading + trailing: the first call in a window fires right away, and if any
// calls land inside the window they collapse into one trailing call at the end
// instead of being dropped outright.
export function createTrailingThrottle(fn: () => void, ms: number) {
  let last = 0;
  let timer: number | undefined;

  const run = () => {
    const elapsed = Date.now() - last;
    if (elapsed >= ms) {
      last = Date.now();
      fn();
      return;
    }
    if (timer === undefined) {
      timer = window.setTimeout(() => {
        timer = undefined;
        last = Date.now();
        fn();
      }, ms - elapsed);
    }
  };

  const cancel = () => {
    window.clearTimeout(timer);
    timer = undefined;
  };

  return { run, cancel };
}

// Drives the same `tick` counter the views already refetch on, but from an SSE
// connection instead of a plain interval. Falls back to polling if the stream
// never opens, drops, or goes quiet.
export function useLiveTick(enabled: boolean): number {
  const [tick, setTick] = useState(0);

  useEffect(() => {
    if (!enabled) return;

    let es: EventSource | null = null;
    let pollTimer: number | undefined;
    let openTimer: number | undefined;
    let silenceTimer: number | undefined;

    const throttle = createTrailingThrottle(() => setTick((n) => n + 1), THROTTLE_MS);
    const bump = throttle.run;

    const startPolling = () => {
      if (pollTimer !== undefined) return;
      pollTimer = window.setInterval(bump, POLL_MS);
    };

    const clearTimers = () => {
      window.clearTimeout(openTimer);
      window.clearTimeout(silenceTimer);
    };

    const fallBack = () => {
      clearTimers();
      es?.close();
      es = null;
      startPolling();
    };

    const armSilenceTimer = () => {
      window.clearTimeout(silenceTimer);
      silenceTimer = window.setTimeout(fallBack, SILENCE_TIMEOUT_MS);
    };

    es = new EventSource(api.streamUrl);

    openTimer = window.setTimeout(() => {
      if (es?.readyState !== EventSource.OPEN) fallBack();
    }, OPEN_TIMEOUT_MS);

    es.onopen = () => {
      window.clearTimeout(openTimer);
      armSilenceTimer();
    };

    es.addEventListener('entry', () => {
      armSilenceTimer();
      bump();
    });

    es.addEventListener('ping', armSilenceTimer);

    es.onerror = () => {
      if (es?.readyState === EventSource.CLOSED) fallBack();
    };

    return () => {
      clearTimers();
      throttle.cancel();
      if (pollTimer !== undefined) window.clearInterval(pollTimer);
      es?.close();
    };
  }, [enabled]);

  return tick;
}
