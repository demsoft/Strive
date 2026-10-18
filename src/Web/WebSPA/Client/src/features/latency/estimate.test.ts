import { describe, expect, test } from 'vitest';
import {
   differsEnough,
   estimateOneWayMs,
   LatencyReport,
   parseJitterBufferMs,
   parseRoundTripMs,
   SEND_PROCESSING_MS,
   verdictFor,
} from './estimate';

const report = (overrides: Partial<LatencyReport> = {}): LatencyReport => ({
   rttMs: 20,
   jitterBufferMs: 30,
   outputMs: 10,
   ...overrides,
});

const statsOf = (entries: Record<string, unknown>[]) =>
   ({ forEach: (fn: (entry: Record<string, unknown>) => void) => entries.forEach(fn) } as unknown as RTCStatsReport);

describe('estimateOneWayMs', () => {
   test('adds the way up, the way down, the buffer and the sound system of the listener', () => {
      const sender = report({ rttMs: 20 });
      const receiver = report({ rttMs: 40, jitterBufferMs: 30, outputMs: 10 });

      expect(estimateOneWayMs(sender, receiver)).toBe(SEND_PROCESSING_MS + 10 + 20 + 30 + 10);
   });

   test('the buffer and sound system of the sender do not matter, only those of the listener', () => {
      const a = estimateOneWayMs(report({ jitterBufferMs: 5, outputMs: 5 }), report());
      const b = estimateOneWayMs(report({ jitterBufferMs: 90, outputMs: 90 }), report());

      expect(a).toBe(b);
   });

   test('it is not the same in both directions', () => {
      const fast = report({ rttMs: 10, jitterBufferMs: 10, outputMs: 5 });
      const slow = report({ rttMs: 60, jitterBufferMs: 80, outputMs: 30 });

      expect(estimateOneWayMs(fast, slow)).not.toBe(estimateOneWayMs(slow, fast));
   });
});

describe('verdictFor', () => {
   test.each([
      [10, 'live'],
      [30, 'live'],
      [31, 'slow'],
      [60, 'slow'],
      [61, 'turns'],
      [200, 'turns'],
   ])('%s ms is %s', (ms, verdict) => {
      expect(verdictFor(ms)).toBe(verdict);
   });
});

describe('parseRoundTripMs', () => {
   test('takes the round trip of the connection in use, in milliseconds', () => {
      const stats = statsOf([
         { type: 'candidate-pair', nominated: false, state: 'failed', currentRoundTripTime: 0.9, bytesSent: 0 },
         { type: 'candidate-pair', nominated: true, state: 'succeeded', currentRoundTripTime: 0.0234, bytesSent: 5000 },
      ]);

      expect(parseRoundTripMs(stats)).toBe(23);
   });

   test('null when nothing is known yet', () => {
      expect(parseRoundTripMs(statsOf([{ type: 'transport' }]))).toBeNull();
   });
});

describe('parseJitterBufferMs', () => {
   test('the average wait of the audio that is received', () => {
      const stats = statsOf([
         { type: 'inbound-rtp', kind: 'audio', jitterBufferDelay: 6, jitterBufferEmittedCount: 100 },
         { type: 'inbound-rtp', kind: 'audio', jitterBufferDelay: 2, jitterBufferEmittedCount: 100 },
         { type: 'inbound-rtp', kind: 'video', jitterBufferDelay: 900, jitterBufferEmittedCount: 10 },
      ]);

      expect(parseJitterBufferMs(stats)).toBe(40);
   });

   test('null without incoming audio', () => {
      expect(parseJitterBufferMs(statsOf([{ type: 'inbound-rtp', kind: 'video' }]))).toBeNull();
   });
});

describe('differsEnough', () => {
   test('small movements are not worth sending', () => {
      expect(differsEnough(report(), report({ rttMs: 24 }))).toBe(false);
      expect(differsEnough(report(), report({ rttMs: 40 }))).toBe(true);
   });
});
