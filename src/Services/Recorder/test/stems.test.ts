import { mkdtemp, readFile, readdir } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { describe, expect, test } from 'vitest';
import { silentLogger } from '../src/logger';
import { buildTracksArchive, planTracks, safeFileName, StemCollector, StemSegment } from '../src/stems';
import { FakeSystem } from './helpers';

const segment = (overrides: Partial<StemSegment>): StemSegment => ({
   id: 1,
   file: '1.webm',
   participantId: 'p1',
   source: 'mic',
   name: 'Ada',
   startedAtMs: 10_000,
   ...overrides,
});

describe('safeFileName', () => {
   test('removes what is not allowed in file names, keeps letters of every language', () => {
      expect(safeFileName('Ada/../Lovelace:*?')).toBe('Ada.Lovelace');
      expect(safeFileName('.hidden')).toBe('hidden');
      expect(safeFileName('Zoë Müller')).toContain('Müller');
   });

   test('never returns an empty name', () => {
      expect(safeFileName('///')).toBe('participant');
   });
});

describe('planTracks', () => {
   test('one track per person and kind of sound, named after both', () => {
      const plans = planTracks(
         [segment({ id: 1 }), segment({ id: 2, file: '2.webm', source: 'instrument' }), segment({ id: 3, file: '3.webm', participantId: 'p2', name: 'Bo' })],
         10_000,
      );

      expect(plans.map((x) => x.fileName)).toEqual(['Ada - voice.flac', 'Ada - instrument.flac', 'Bo - voice.flac']);
   });

   test('two people with the same name get different files', () => {
      const plans = planTracks([segment({ id: 1 }), segment({ id: 2, file: '2.webm', participantId: 'p2' })], 0);

      expect(plans.map((x) => x.fileName)).toEqual(['Ada - voice.flac', 'Ada - voice (2).flac']);
   });

   test('the pieces of a track are in time order and placed relative to the start of the mix', () => {
      const plans = planTracks(
         [segment({ id: 2, file: '2.webm', startedAtMs: 50_000 }), segment({ id: 1, file: '1.webm', startedAtMs: 8_000 })],
         10_000,
      );

      expect(plans[0].inputs).toEqual([
         { file: '1.webm', offsetMs: -2000 },
         { file: '2.webm', offsetMs: 40_000 },
      ]);
   });
});

describe('StemCollector', () => {
   test('writes the chunks of a piece in order and keeps a list of the pieces', async () => {
      const dir = await mkdtemp(join(tmpdir(), 'stems-test-'));
      const collector = new StemCollector(dir, silentLogger);

      const id = await collector.start({ participantId: 'p1', source: 'mic', name: 'Ada', startedAtMs: 5 });
      await collector.chunk(id, Buffer.from('abc').toString('base64'));
      await collector.chunk(id, Buffer.from('def').toString('base64'));
      await collector.end(id, 99);

      expect(await readFile(join(dir, 'stems', `${id}.webm`), 'utf8')).toBe('abcdef');
      const list = JSON.parse(await readFile(join(dir, 'stems', 'segments.json'), 'utf8'));
      expect(list).toEqual([expect.objectContaining({ id, participantId: 'p1', startedAtMs: 5, endedAtMs: 99 })]);
   });

   test('data for an unknown piece is ignored', async () => {
      const dir = await mkdtemp(join(tmpdir(), 'stems-test-'));
      const collector = new StemCollector(dir, silentLogger);

      await collector.chunk(42, 'AAAA');

      expect(await readdir(dir)).toEqual([]);
   });
});

describe('buildTracksArchive', () => {
   async function prepared() {
      const dir = await mkdtemp(join(tmpdir(), 'stems-test-'));
      const collector = new StemCollector(dir, silentLogger);
      for (const [participantId, source, name] of [
         ['p1', 'mic', 'Ada'],
         ['p1', 'instrument', 'Ada'],
      ]) {
         const id = await collector.start({ participantId, source, name, startedAtMs: 12_000 });
         await collector.chunk(id, Buffer.alloc(5000).toString('base64'));
         await collector.end(id, 13_000);
      }
      return dir;
   }

   test('makes a file for every track and packs them into one archive', async () => {
      const dir = await prepared();
      const system = new FakeSystem();

      const archive = await buildTracksArchive(dir, 10_000, 61.25, { system, log: silentLogger });

      expect(archive).toBe(join(dir, 'tracks.zip'));
      const ffmpegCalls = system.runCalls.filter(([c]) => c === 'ffmpeg');
      expect(ffmpegCalls).toHaveLength(2);
      expect(ffmpegCalls[0][1].join(' ')).toContain('adelay=2000|2000');
      const zip = system.runCalls.find(([c]) => c === 'zip')!;
      expect(zip[1].slice(0, 3)).toEqual(['-0', '-j', '-q']);
      expect(zip[1].filter((x) => x.endsWith('.flac')).map((x) => x.split('/').pop())).toEqual([
         'Ada - voice.flac',
         'Ada - instrument.flac',
      ]);
   });

   test('nothing was recorded: no archive', async () => {
      const dir = await mkdtemp(join(tmpdir(), 'stems-test-'));

      expect(await buildTracksArchive(dir, 0, 10, { system: new FakeSystem(), log: silentLogger })).toBeNull();
   });
});

describe('buildTracksArchive (broken pieces)', () => {
   test('a piece without sound is left out', async () => {
      const dir = await mkdtemp(join(tmpdir(), 'stems-test-'));
      const collector = new StemCollector(dir, silentLogger);
      const good = await collector.start({ participantId: 'p1', source: 'mic', name: 'Ada', startedAtMs: 12_000 });
      await collector.chunk(good, Buffer.alloc(5000).toString('base64'));
      await collector.end(good, 13_000);
      const empty = await collector.start({ participantId: 'p1', source: 'mic', name: 'Ada', startedAtMs: 14_000 });
      await collector.chunk(empty, Buffer.from('hdr').toString('base64'));
      await collector.end(empty, 14_100);
      const system = new FakeSystem();

      await buildTracksArchive(dir, 10_000, 30, { system, log: silentLogger });

      const call = system.runCalls.find(([c]) => c === 'ffmpeg')![1];
      expect(call.filter((x) => x === '-i')).toHaveLength(1);
   });
});
