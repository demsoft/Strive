import { describe, expect, test } from 'vitest';
import { buildProbeArgs, buildRecordArgs, buildRemuxArgs, parseDuration } from '../src/ffmpeg';

const options = { display: ':101', sink: 'rec_101', outFile: '/data/r/out.mp4', width: 1280, height: 720, fps: 25, crf: 26, maxSeconds: 7200 };

describe('buildRecordArgs', () => {
   const args = buildRecordArgs(options);
   const after = (flag: string) => args[args.indexOf(flag) + 1];

   test('captures the virtual screen and the monitor of the sound sink', () => {
      expect(args.slice(args.indexOf('x11grab') - 1, args.indexOf('x11grab') + 1)).toEqual(['-f', 'x11grab']);
      expect(args).toContain(':101.0');
      expect(args).toContain('rec_101.monitor');
      expect(after('-video_size')).toBe('1280x720');
      expect(after('-framerate')).toBe('25');
   });

   test('encodes h264 and aac with a keyframe every two seconds', () => {
      expect(after('-c:v')).toBe('libx264');
      expect(after('-c:a')).toBe('aac');
      expect(after('-g')).toBe('50');
      expect(after('-crf')).toBe('26');
      expect(after('-pix_fmt')).toBe('yuv420p');
   });

   test('writes a fragmented mp4 so a crash does not lose the recording', () => {
      expect(after('-movflags')).toContain('frag_keyframe');
      expect(after('-movflags')).toContain('empty_moov');
   });

   test('has a time limit and writes the output file last', () => {
      expect(after('-t')).toBe('7200');
      expect(args[args.length - 1]).toBe('/data/r/out.mp4');
   });

   test('does not draw the mouse', () => {
      expect(after('-draw_mouse')).toBe('0');
   });

   test('keeps audio and video in step with the clock of the machine', () => {
      expect(after('-use_wallclock_as_timestamps')).toBe('1');
      expect(after('-af')).toContain('aresample=async=1');
      // the audio input is the one that uses the wall clock
      expect(args.indexOf('-use_wallclock_as_timestamps')).toBeGreaterThan(args.indexOf('x11grab'));
      expect(args.indexOf('-use_wallclock_as_timestamps')).toBeLessThan(args.indexOf('pulse'));
   });
});

test('buildRemuxArgs copies the streams and moves the index to the start', () => {
   const args = buildRemuxArgs('in.mp4', 'out.mp4');

   expect(args).toEqual(expect.arrayContaining(['-c', 'copy', '+faststart']));
   expect(args[args.indexOf('-i') + 1]).toBe('in.mp4');
   expect(args[args.length - 1]).toBe('out.mp4');
});

test('buildProbeArgs asks for the duration only', () => {
   expect(buildProbeArgs('f.mp4')).toEqual(expect.arrayContaining(['format=duration', 'f.mp4']));
});

describe('parseDuration', () => {
   test.each([
      ['61.25\n', 61.25],
      ['0', 0],
      ['3725.5', 3725.5],
   ])('%j -> %d', (output, expected) => expect(parseDuration(output)).toBe(expected));

   test.each(['N/A', '', 'abc', '-3'])('%j -> null', (output) => expect(parseDuration(output)).toBeNull());
});
