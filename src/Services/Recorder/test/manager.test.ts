import { mkdir, writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import { describe, expect, test, vi } from 'vitest';
import { silentLogger } from '../src/logger';
import { RecorderManager, SessionFactory, validateCommand } from '../src/manager';
import { DuplicateRecordingError, RecorderBusyError } from '../src/types';
import { command, FakeReporter, FakeSystem, FakeUploader, testConfig } from './helpers';

class FakeSession {
   started = vi.fn(async () => undefined);
   stopped = vi.fn(async (_reason: string) => undefined);
   constructor(readonly command: ReturnType<typeof command>, readonly display: number) {}
   start() {
      return this.started();
   }
   stop(reason: string) {
      return this.stopped(reason);
   }
}

async function create(configOverrides = {}) {
   const config = await testConfig(configOverrides);
   const reporter = new FakeReporter();
   const uploader = new FakeUploader();
   const system = new FakeSystem();
   const sessions: FakeSession[] = [];
   const factory: SessionFactory = (cmd, display) => {
      const session = new FakeSession(cmd, display);
      sessions.push(session);
      return session as never;
   };
   const manager = new RecorderManager({ config, system, reporter, uploader, log: silentLogger }, silentLogger, factory);
   return { manager, reporter, sessions, config, uploader, system };
}

describe('validateCommand', () => {
   test('accepts a normal command', () => expect(validateCommand(command())).toBeNull());

   test.each([
      [{ recordingId: '../etc' }, 'recordingId'],
      [{ recordingId: '' }, 'recordingId'],
      [{ conferenceId: 'a/b' }, 'conferenceId'],
      [{ joinToken: 'short' }, 'joinToken'],
      [{ storageKey: '../../secret.mp4' }, 'storageKey'],
      [{ storageKey: 'other/conf1/rec1.mp4' }, 'storageKey'],
      [{ storageKey: 'recordings/conf1/rec1.exe' }, 'storageKey'],
      [{ maxDurationMinutes: 0 }, 'maxDurationMinutes'],
      [{ maxDurationMinutes: 100000 }, 'maxDurationMinutes'],
      [{ maxDurationMinutes: 1.5 }, 'maxDurationMinutes'],
   ])('rejects %j', (overrides, field) => {
      expect(validateCommand(command(overrides))).toContain(field);
   });
});

describe('RecorderManager', () => {
   test('start accepts the recording and starts the session in the background', async () => {
      const { manager, sessions } = await create();

      manager.start(command());

      expect(manager.activeCount).toBe(1);
      expect(manager.has('rec1')).toBe(true);
      expect(sessions[0].started).toHaveBeenCalled();
   });

   test('invalid commands are rejected before anything starts', async () => {
      const { manager, sessions } = await create();

      expect(() => manager.start(command({ recordingId: '../x' }))).toThrowError(RangeError);
      expect(sessions).toHaveLength(0);
   });

   test('the same recording cannot be started twice', async () => {
      const { manager } = await create();
      manager.start(command());

      expect(() => manager.start(command())).toThrowError(DuplicateRecordingError);
   });

   test('refuses recordings above the capacity', async () => {
      const { manager } = await create({ maxConcurrentRecordings: 1 });
      manager.start(command());

      expect(() => manager.start(command({ recordingId: 'rec2' }))).toThrowError(RecorderBusyError);
   });

   test('every session gets its own display', async () => {
      const { manager, sessions } = await create();

      manager.start(command({ recordingId: 'a' }));
      manager.start(command({ recordingId: 'b' }));

      expect(new Set(sessions.map((s) => s.display)).size).toBe(2);
   });

   test('a display is reused after the recording ended', async () => {
      const { manager, sessions } = await create();
      manager.start(command({ recordingId: 'a' }));
      await manager.stop('a');

      manager.start(command({ recordingId: 'b' }));

      expect(sessions[1].display).toBe(sessions[0].display);
   });

   test('a start failure is reported to the server and frees the slot', async () => {
      const { manager, reporter } = await create();
      const failing: SessionFactory = (cmd) =>
         ({ command: cmd, start: async () => { throw new Error('browser crashed'); }, stop: async () => undefined }) as never;
      const failingManager = new RecorderManager(
         { config: await testConfig(), system: new FakeSystem(), reporter, uploader: new FakeUploader(), log: silentLogger },
         silentLogger,
         failing,
      );

      failingManager.start(command());

      await vi.waitFor(() => expect(reporter.reports).toHaveLength(1));
      expect(reporter.reports[0]).toEqual({ id: 'rec1', report: { event: 'failed', reason: 'The recorder could not join the conference.' } });
      expect(failingManager.activeCount).toBe(0);
      expect(manager.activeCount).toBe(0);
   });

   test('stop stops the session and frees the slot', async () => {
      const { manager, sessions } = await create();
      manager.start(command());

      const stopped = await manager.stop('rec1');

      expect(stopped).toBe(true);
      expect(sessions[0].stopped).toHaveBeenCalledWith('requested');
      expect(manager.activeCount).toBe(0);
   });

   test('stopping an unknown recording is not an error', async () => {
      const { manager } = await create();

      expect(await manager.stop('nope')).toBe(false);
   });

   test('shutdown stops everything', async () => {
      const { manager, sessions } = await create();
      manager.start(command({ recordingId: 'a' }));
      manager.start(command({ recordingId: 'b' }));

      await manager.shutdown();

      expect(sessions.every((s) => s.stopped.mock.calls[0][0] === 'shutdown')).toBe(true);
      expect(manager.activeCount).toBe(0);
   });

   describe('recover', () => {
      async function leftover(config: { workDir: string }, id: string, files: Record<string, string | Buffer>) {
         const dir = join(config.workDir, id);
         await mkdir(dir, { recursive: true });
         for (const [name, content] of Object.entries(files)) await writeFile(join(dir, name), content);
      }

      const meta = (id: string) => JSON.stringify({ recordingId: id, conferenceId: 'conf1', storageKey: `recordings/conf1/${id}.mp4`, startedAt: '' });

      test('uploads and reports recordings an earlier recorder left behind', async () => {
         const { manager, config, uploader, reporter } = await create();
         await leftover(config, 'old1', { 'meta.json': meta('old1'), 'out.mp4': Buffer.alloc(50_000) });

         const recovered = await manager.recover();

         expect(recovered).toBe(1);
         expect(uploader.uploads[0].key).toBe('recordings/conf1/old1.mp4');
         expect(reporter.reports.at(-1)?.report.event).toBe('finished');
      });

      test('ignores finished, foreign and running directories', async () => {
         const { manager, config, uploader } = await create();
         await leftover(config, 'done1', { 'meta.json': meta('done1'), 'out.mp4': Buffer.alloc(50_000), done: '' });
         await leftover(config, 'foreign', { 'other.txt': 'x' });
         manager.start(command({ recordingId: 'running' }));
         await leftover(config, 'running', { 'meta.json': meta('running'), 'out.mp4': Buffer.alloc(50_000) });

         expect(await manager.recover()).toBe(0);
         expect(uploader.upload).not.toHaveBeenCalled();
      });

      test('nothing to recover without a work directory', async () => {
         const { manager } = await create({ workDir: '/does/not/exist' });

         expect(await manager.recover()).toBe(0);
      });
   });
});
