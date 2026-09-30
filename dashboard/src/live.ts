import { useEffect, useRef, useState } from 'react';
import { api } from './api';

const POLL_MS = 5000;
const THROTTLE_MS = 1000;
const OPEN_TIMEOUT_MS = 5000;
const SILENCE_TIMEOUT_MS = 45000;

// Drives the same `tick` counter the views already refetch on, but from an SSE
// connection instead of a plain interval. Falls back to polling if the stream
// never opens, drops, or goes quiet.
export function useLiveTick(enabled: boolean): number {
  const [tick, setTick] = useState(0);
  const lastBump = useRef(0);

  useEffect(() => {
    if (!enabled) return;

    let es: EventSource | null = null;
    let pollTimer: number | undefined;
    let openTimer: number | undefined;
    let silenceTimer: number | undefined;

    const bump = () => {
      const now = Date.now();
      if (now - lastBump.current < THROTTLE_MS) return;
      lastBump.current = now;
      setTick((n) => n + 1);
    };

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
      if (pollTimer !== undefined) window.clearInterval(pollTimer);
      es?.close();
    };
  }, [enabled]);

  return tick;
}
