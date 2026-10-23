import { describe, expect, test } from 'vitest';
import { MAX_VIDEO_BYTES } from './limits';
import { fileProblem, formatDuration, formatMegabytes } from './videoFile';

describe('fileProblem', () => {
   test('a normal video is fine, also when the browser cannot tell how long it is', () => {
      expect(fileProblem({ type: 'video/mp4', size: 50_000_000 }, 120)).toBeNull();
      expect(fileProblem({ type: 'video/quicktime', size: 50_000_000 }, null)).toBeNull();
   });

   test('five minutes exactly is fine, a second more is too long', () => {
      expect(fileProblem({ type: 'video/mp4', size: 1000 }, 300)).toBeNull();
      expect(fileProblem({ type: 'video/mp4', size: 1000 }, 300.5)).toBe('too_long');
   });

   test.each(['image/png', 'audio/mpeg', 'application/pdf', ''])('%s is not a video', (type) => {
      expect(fileProblem({ type, size: 1000 }, 10)).toBe('not_video');
   });

   test('an empty file or one over the limit is too big', () => {
      expect(fileProblem({ type: 'video/mp4', size: 0 }, 10)).toBe('too_big');
      expect(fileProblem({ type: 'video/mp4', size: MAX_VIDEO_BYTES }, 10)).toBeNull();
      expect(fileProblem({ type: 'video/mp4', size: MAX_VIDEO_BYTES + 1 }, 10)).toBe('too_big');
   });
});

describe('formatDuration', () => {
   test.each([
      [95, '1:35'],
      [3, '0:03'],
      [300, '5:00'],
      [59.6, '1:00'],
      [0, '0:00'],
   ])('%d seconds is %s', (seconds, text) => expect(formatDuration(seconds)).toBe(text));

   test('unknown or nonsense is empty', () => {
      expect(formatDuration(null)).toBe('');
      expect(formatDuration(undefined)).toBe('');
      expect(formatDuration(NaN)).toBe('');
      expect(formatDuration(-1)).toBe('');
   });
});

test('formatMegabytes rounds, and a small file is at least 1 MB', () => {
   expect(formatMegabytes(50 * 1024 * 1024)).toBe('50 MB');
   expect(formatMegabytes(10)).toBe('1 MB');
});
