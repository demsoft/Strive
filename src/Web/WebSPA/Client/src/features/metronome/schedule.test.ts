import { describe, expect, test } from 'vitest';
import {
   beatInBar,
   beatIntervalMs,
   beatTimeMs,
   clampBpm,
   isAccent,
   MetronomeSession,
   nextBeatIndex,
   planBeats,
} from './schedule';

const FIRST = Date.parse('2026-10-18T12:00:00.000Z');
const session = (overrides: Partial<MetronomeSession> = {}): MetronomeSession => ({
   bpm: 120,
   beatsPerBar: 4,
   startedBy: 'teacher',
   firstBeatAt: new Date(FIRST).toISOString(),
   ...overrides,
});

describe('beat times', () => {
   test('120 BPM is a beat every half second', () => {
      expect(beatIntervalMs(120)).toBe(500);
      expect(beatTimeMs(session(), 0)).toBe(FIRST);
      expect(beatTimeMs(session(), 3)).toBe(FIRST + 1500);
   });

   test('every computer finds the same beats from the same description', () => {
      expect(beatTimeMs(session({ bpm: 90 }), 7)).toBe(FIRST + 7 * (60000 / 90));
   });
});

describe('nextBeatIndex', () => {
   test('before the first beat the first beat is next', () => {
      expect(nextBeatIndex(session(), FIRST - 800)).toBe(0);
   });

   test('on a beat that beat is meant', () => {
      expect(nextBeatIndex(session(), FIRST + 1000)).toBe(2);
   });

   test('between beats the following beat is next, however long it has been running', () => {
      expect(nextBeatIndex(session(), FIRST + 1100)).toBe(3);
      expect(nextBeatIndex(session(), FIRST + 60 * 60 * 1000 + 100)).toBe(7201);
   });
});

describe('accent and counting', () => {
   test('the first beat of the bar is accented and counted 1', () => {
      expect(isAccent(0, 4)).toBe(true);
      expect(isAccent(4, 4)).toBe(true);
      expect(isAccent(3, 4)).toBe(false);
      expect(beatInBar(0, 4)).toBe(1);
      expect(beatInBar(6, 4)).toBe(3);
   });

   test('one beat per bar: every beat is accented', () => {
      expect(isAccent(5, 1)).toBe(true);
   });
});

describe('planBeats', () => {
   test('hands over the beats that are due within the look-ahead, with the time until each', () => {
      const planned = planBeats(session(), FIRST + 400, 0, 700);

      expect(planned.map((x) => x.index)).toEqual([1, 2]);
      expect(planned[0].inMs).toBe(100);
      expect(planned[1].inMs).toBe(600);
   });

   test('a beat is not planned twice', () => {
      const first = planBeats(session(), FIRST + 400, 0, 700);
      const second = planBeats(session(), FIRST + 450, first.at(-1)!.index + 1, 1200);

      expect(second.map((x) => x.index)).toEqual([3]);
   });

   test('beats of a click that starts in the future wait for it', () => {
      const planned = planBeats(session(), FIRST - 1000, 0, 300);

      expect(planned).toEqual([]);
      expect(planBeats(session(), FIRST - 200, 0, 300)[0]).toEqual({ index: 0, inMs: 200, accent: true });
   });

   test('marks the accent of every bar', () => {
      const planned = planBeats(session({ beatsPerBar: 3 }), FIRST, 0, 1600);

      expect(planned.map((x) => x.accent)).toEqual([true, false, false, true]);
   });
});

describe('clampBpm', () => {
   test.each([
      [10, 30],
      [500, 300],
      [99.6, 100],
      [Number.NaN, 30],
   ])('%s becomes %s', (input, expected) => {
      expect(clampBpm(input)).toBe(expected);
   });
});
