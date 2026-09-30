import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { createTrailingThrottle } from './live';

describe('createTrailingThrottle', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('fires the first call immediately', () => {
    const fn = vi.fn();
    createTrailingThrottle(fn, 1000).run();
    expect(fn).toHaveBeenCalledTimes(1);
  });

  it('schedules a trailing call instead of dropping one that arrives mid-window', () => {
    const fn = vi.fn();
    const throttle = createTrailingThrottle(fn, 1000);

    throttle.run(); // leading, fires now
    vi.advanceTimersByTime(400);
    throttle.run(); // suppressed, but scheduled for the end of the window

    expect(fn).toHaveBeenCalledTimes(1);

    vi.advanceTimersByTime(600);
    expect(fn).toHaveBeenCalledTimes(2);
  });

  it('coalesces a burst into a single trailing call', () => {
    const fn = vi.fn();
    const throttle = createTrailingThrottle(fn, 1000);

    throttle.run();
    for (let i = 0; i < 10; i++) {
      vi.advanceTimersByTime(50);
      throttle.run();
    }

    expect(fn).toHaveBeenCalledTimes(1);

    vi.advanceTimersByTime(1000);
    expect(fn).toHaveBeenCalledTimes(2);
  });

  it('cancel prevents a pending trailing call', () => {
    const fn = vi.fn();
    const throttle = createTrailingThrottle(fn, 1000);

    throttle.run();
    vi.advanceTimersByTime(400);
    throttle.run();
    throttle.cancel();

    vi.advanceTimersByTime(1000);
    expect(fn).toHaveBeenCalledTimes(1);
  });
});
