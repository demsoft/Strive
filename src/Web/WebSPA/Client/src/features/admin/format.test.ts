import { describe, expect, it } from 'vitest';
import { formatAgo, formatBytes, formatUptime, toneFor, toneForFree } from './format';

describe('formatBytes', () => {
   it.each([
      [0, '0 B'],
      [999, '999 B'],
      [1500, '1.5 KB'],
      [16_000_000_000, '16.0 GB'],
      [250_000_000_000, '250 GB'],
      [1_200_000_000_000, '1.2 TB'],
   ])('%d -> %s', (bytes, expected) => expect(formatBytes(bytes)).toBe(expected));

   it('has a dash for nothing', () => {
      expect(formatBytes(null)).toBe('–');
      expect(formatBytes(undefined)).toBe('–');
      expect(formatBytes(NaN)).toBe('–');
   });
});

describe('formatUptime', () => {
   it.each([
      [30, '1m'],
      [90, '1m'],
      [3700, '1h 1m'],
      [93784, '1d 2h'],
   ])('%d -> %s', (seconds, expected) => expect(formatUptime(seconds)).toBe(expected));

   it('has a dash for nonsense', () => expect(formatUptime(-1)).toBe('–'));
});

describe('formatAgo', () => {
   const now = Date.parse('2026-01-01T12:00:00Z');

   it.each([
      ['2026-01-01T11:59:40Z', 'just now'],
      ['2026-01-01T11:55:00Z', '5 min'],
      ['2026-01-01T10:00:00Z', '2 h'],
      ['2025-12-29T12:00:00Z', '3 d'],
   ])('%s -> %s', (iso, expected) => expect(formatAgo(iso, now)).toBe(expected));

   it('has a dash without a time', () => expect(formatAgo(null, now)).toBe('–'));
   it('a time in the future is just now', () => expect(formatAgo('2026-01-01T12:05:00Z', now)).toBe('just now'));
});

describe('tones', () => {
   it('grow: ok below the warning, critical from the critical value on', () => {
      expect(toneFor(69, 70, 90)).toBe('ok');
      expect(toneFor(70, 70, 90)).toBe('warning');
      expect(toneFor(90, 70, 90)).toBe('critical');
      expect(toneFor(null, 70, 90)).toBe('ok');
   });

   it('shrink: free space', () => {
      expect(toneForFree(50, 15, 5)).toBe('ok');
      expect(toneForFree(15, 15, 5)).toBe('warning');
      expect(toneForFree(5, 15, 5)).toBe('critical');
   });
});
