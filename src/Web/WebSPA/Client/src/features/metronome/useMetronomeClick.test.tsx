import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

import useMetronomeClick from './useMetronomeClick';
import { MetronomeSession } from './schedule';

const START = Date.parse('2026-10-18T12:00:00.000Z');
let now = START - 1000;
const clicks: { when: number; frequency: number }[] = [];
class FakeAudioContext {
   state = 'running';
   /** the audio clock runs with the (test) clock */
   get currentTime() {
      return 100 + (now - START) / 1000;
   }
   baseLatency = 0;
   outputLatency = 0;
   destination = {};
   resume = vi.fn(async () => undefined);
   close = vi.fn(async () => undefined);
   createGain() {
      return { gain: { setValueAtTime: vi.fn(), exponentialRampToValueAtTime: vi.fn() }, connect: vi.fn() };
   }
   createOscillator() {
      const osc = {
         frequency: { value: 0 },
         connect: vi.fn(),
         stop: vi.fn(),
         start: (when: number) => clicks.push({ when, frequency: osc.frequency.value }),
      };
      return osc;
   }
}

const session: MetronomeSession = {
   bpm: 120,
   beatsPerBar: 4,
   startedBy: 't',
   firstBeatAt: new Date(START).toISOString(),
};

let container: HTMLDivElement;
let beat: number | null = null;
function Probe({ muted }: { muted: boolean }) {
   beat = useMetronomeClick(session, () => now, 0.7, muted);
   return null;
}

beforeEach(() => {
   vi.useFakeTimers();
   clicks.length = 0;
   now = START - 1000;
   vi.stubGlobal('AudioContext', FakeAudioContext);
   container = document.createElement('div');
   document.body.appendChild(container);
});
afterEach(() => {
   vi.useRealTimers();
   vi.unstubAllGlobals();
   container.remove();
});

const advance = async (ms: number) => {
   for (let i = 0; i < ms / 50; i++) {
      now += 50;
      await act(async () => vi.advanceTimersByTimeAsync(50));
   }
};

test('clicks are handed to the audio clock ahead of time, an accent on the first beat of the bar', async () => {
   act(() => createRoot(container).render(<Probe muted={false} />));

   await advance(2000);

   // beats at +0, +500, +1000 relative to START; the first one is 1000 ms ahead of the test start
   expect(clicks.length).toBeGreaterThanOrEqual(3);
   expect(clicks[0].frequency).toBe(1600);
   expect(clicks[1].frequency).toBe(1000);
   expect(clicks[1].when - clicks[0].when).toBeCloseTo(0.5, 2);
});

test('a muted click is silent here but the count goes on', async () => {
   act(() => createRoot(container).render(<Probe muted />));

   await advance(1200);

   expect(clicks).toHaveLength(0);
   expect(beat).not.toBeNull();
});

test('no beat is played twice', async () => {
   act(() => createRoot(container).render(<Probe muted={false} />));

   await advance(2200);

   const times = clicks.map((x) => x.when.toFixed(3));
   expect(new Set(times).size).toBe(times.length);
});
