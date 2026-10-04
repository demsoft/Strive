import { readdir, stat } from 'node:fs/promises';
import { join } from 'node:path';
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest';
import { silentLogger } from '../src/logger';
import { RecordingSession } from '../src/session';
import { command, FakeReporter, FakeSystem, FakeUploader, testConfig } from './helpers';

async function create(overrides: { system?: FakeSystem; uploader?: FakeUploader; cmd?: Partial<ReturnType<typeof command>> } = {}) {
   const config = await testConfig();
   const system = overrides.system ?? new FakeSystem();
   const reporter = new FakeReporter();
   const uploader = overrides.uploader ?? new FakeUploader();
   const session = new RecordingSession(command(overrides.cmd), 101, { config, system, reporter, uploader, log: silentLogger, retryDelayMs: 1000 });
   return { config, system, reporter, uploader, session };
}

const exists = (path: string) => stat(path).then(() => true, () => false);

describe('RecordingSession', () => {
   beforeEach(() => vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] }));
   afterEach(() => vi.useRealTimers());

   /**
    * Wait for a promise that sleeps on the fake clock and does real file I/O in between: keep moving the clock
    * until it is settled.
    */
   async function settle<T>(promise: Promise<T>, stepMs = 1000): Promise<T> {
      let done = false;
      promise.then(
         () => (done = true),
         () => (done = true),
      );
      while (!done) {
         await vi.advanceTimersByTimeAsync(stepMs);
         await new Promise((resolve) => setImmediate(resolve));
      }
      return promise;
   }

   const start = (session: RecordingSession) => settle(session.start());

   test('start joins through the recording view with the token in the fragment', async () => {
      const { session, system, reporter } = await create();

      await start(session);

      expect(system.launched).toHaveLength(1);
      expect(system.launched[0].url).toBe(`https://localhost/c/conf1/recording#token=${'a'.repeat(40)}`);
      expect(system.launched[0].display).toBe(':101');
      expect(system.launched[0].sink).toBe('rec_101');
      // every session has its own debugging port
      expect(system.launched[0].debugPort).toBe(9401);
      expect(reporter.events()).toEqual(['started']);
   });

   test('stop finishes ffmpeg, makes the file seekable, uploads it and reports', async () => {
      const { session, system, reporter, uploader, config } = await create();
      await start(session);

      await settle(session.stop('requested'));

      expect(system.ffmpeg.quitCalled).toBe(true);
      expect(system.browserClosed).toBe(true);
      expect(system.sinkRemoved).toBe(true);
      expect(system.xvfb.killed).toBe(true);
      expect(system.runCalls.map(([c]) => c)).toEqual(['ffmpeg', 'ffprobe']);
      expect(uploader.uploads).toEqual([{ file: join(config.workDir, 'rec1', 'final.mp4'), key: 'recordings/conf1/rec1.mp4' }]);
      expect(reporter.reports.at(-1)).toEqual({
         id: 'rec1',
         report: { event: 'finished', storageKey: 'recordings/conf1/rec1.mp4', sizeBytes: 123456, durationSeconds: 61.25 },
      });
      // nothing is left on the disk after a successful upload
      expect(await exists(join(config.workDir, 'rec1'))).toBe(false);
   });

   test('stop is idempotent', async () => {
      const { session, uploader } = await create();
      await start(session);

      await settle(Promise.all([session.stop('a'), session.stop('b')]));
      await settle(session.stop('c'));

      expect(uploader.upload).toHaveBeenCalledTimes(1);
   });

   test('a failed start cleans up everything and rethrows', async () => {
      const system = new FakeSystem();
      system.failReady = true;
      const { session, config } = await create({ system });

      const caught = await settle(session.start().catch((e) => e));

      expect(String(caught)).toContain('not ready');
      expect(system.browserClosed).toBe(true);
      expect(system.sinkRemoved).toBe(true);
      expect(system.xvfb.killed).toBe(true);
      expect(await exists(join(config.workDir, 'rec1'))).toBe(false);
   });

   test('ffmpeg dying right away is a failed start', async () => {
      const system = new FakeSystem();
      system.ffmpeg.exit(1);
      const { session } = await create({ system });

      const caught = await settle(session.start().catch((e) => e));

      expect(String(caught)).toContain('ffmpeg stopped right after it started');
   });

   test('the recording ends by itself after the maximum duration', async () => {
      const { session, system, reporter } = await create({ cmd: { maxDurationMinutes: 2 } });
      await start(session);

      await settle(Promise.resolve().then(() => vi.advanceTimersByTimeAsync(2 * 60 * 1000 + 100)));

      expect(system.ffmpeg.quitCalled).toBe(true);
      await vi.waitFor(() => expect(reporter.events()).toContain('finished'), { timeout: 5000, interval: 20 });
   });

   test('a capture that dies mid-recording still stores what was recorded', async () => {
      const { session, system, reporter } = await create();
      await start(session);

      system.ffmpeg.exit(1);

      await vi.waitFor(() => expect(reporter.events()).toContain('finished'));
   });

   test('a crashing browser ends the recording and stores what exists', async () => {
      const { session, system, reporter } = await create();
      await start(session);

      system.crashBrowser();

      await vi.waitFor(() => expect(reporter.events()).toContain('finished'));
   });

   test('ffmpeg that ignores the quit request is killed', async () => {
      const system = new FakeSystem();
      system.ffmpeg = new (system.ffmpeg.constructor as new (exitOnQuit: boolean) => typeof system.ffmpeg)(false);
      const { session, reporter } = await create({ system });
      await start(session);

      await settle(session.stop('requested'), 5000);

      expect(system.ffmpeg.killed).toBe(true);
      expect(reporter.events()).toContain('finished');
   });

   test('an empty recording is reported as failed and removed', async () => {
      const system = new FakeSystem();
      system.recordedBytes = 100;
      const { session, reporter, config, uploader } = await create({ system });
      await start(session);

      await settle(session.stop('requested'));

      expect(reporter.reports.at(-1)?.report).toEqual({ event: 'failed', reason: 'The recording is empty.' });
      expect(uploader.upload).not.toHaveBeenCalled();
      expect(await exists(join(config.workDir, 'rec1'))).toBe(false);
   });

   test('when making the file seekable fails the original is uploaded', async () => {
      const system = new FakeSystem();
      system.failRemux = true;
      const { session, uploader, reporter, config } = await create({ system });
      await start(session);

      await settle(session.stop('requested'));

      expect(uploader.uploads[0].file).toBe(join(config.workDir, 'rec1', 'out.mp4'));
      expect(reporter.events()).toContain('finished');
   });

   test('upload failures are retried', async () => {
      const uploader = new FakeUploader();
      uploader.failures = 2;
      const { session, reporter } = await create({ uploader });
      await start(session);

      await settle(session.stop('requested'));

      expect(uploader.upload).toHaveBeenCalledTimes(3);
      expect(reporter.reports.at(-1)?.report.event).toBe('finished');
   });

   test('when the upload keeps failing the file stays and the failure is reported', async () => {
      const uploader = new FakeUploader();
      uploader.failures = 99;
      const { session, reporter, config } = await create({ uploader });
      await start(session);

      await settle(session.stop('requested'));

      expect(reporter.reports.at(-1)?.report).toEqual({ event: 'failed', reason: 'The recording could not be uploaded.' });
      const files = await readdir(join(config.workDir, 'rec1'));
      expect(files).toContain('out.mp4');
      // marked as handled, so that the recovery does not report it again
      expect(files).toContain('done');
   });
});
