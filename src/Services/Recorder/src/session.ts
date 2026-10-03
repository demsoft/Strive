import { rm } from 'node:fs/promises';
import { join } from 'node:path';
import { Config } from './config';
import { buildRecordArgs } from './ffmpeg';
import { finalizeRecording, OUT_FILE, RecordingMeta, writeMeta } from './finalize';
import { Logger } from './logger';
import { BrowserHandle, RunningProcess, SystemOps } from './system';
import { Reporter, StartCommand, Uploader } from './types';

export type SessionDeps = {
   config: Config;
   system: SystemOps;
   reporter: Reporter;
   uploader: Uploader;
   log: Logger;
   /** pause between upload attempts, a fixed value is used in tests */
   retryDelayMs?: number;
};

/** global timers, so that tests can move the clock */
const sleep = (ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms));

const READY_TIMEOUT_MS = 90_000;
const FFMPEG_STOP_TIMEOUT_MS = 20_000;
/** ffmpeg exiting this soon after it started means it could not capture at all */
const FFMPEG_STARTUP_CHECK_MS = 1500;

/** One recording: a virtual screen, a virtual sound card, a browser in the conference and ffmpeg capturing both. */
export class RecordingSession {
   private xvfb?: RunningProcess;
   private removeSink?: () => Promise<void>;
   private browser?: BrowserHandle;
   private ffmpeg?: RunningProcess;
   private maxDurationTimer?: ReturnType<typeof setTimeout>;
   private stopping?: Promise<void>;

   readonly dir: string;

   /** @param displayNumber an X display no other session uses */
   constructor(
      readonly command: StartCommand,
      private readonly displayNumber: number,
      private readonly deps: SessionDeps,
   ) {
      this.dir = join(deps.config.workDir, command.recordingId);
   }

   private get display() {
      return `:${this.displayNumber}`;
   }

   private get sinkName() {
      return `rec_${this.displayNumber}`;
   }

   /** Joins the conference and starts capturing. On failure everything is cleaned up and the error is thrown. */
   async start(): Promise<void> {
      const { config, system, log } = this.deps;
      const { command } = this;

      try {
         await writeMeta(this.dir, {
            recordingId: command.recordingId,
            conferenceId: command.conferenceId,
            storageKey: command.storageKey,
            startedAt: new Date().toISOString(),
         });

         this.xvfb = await system.startXvfb(this.display, config.video.width, config.video.height);
         this.removeSink = await system.createSink(this.sinkName);

         // the token is in the fragment: it never leaves the browser, no server logs it
         const url = `${config.webUrl}/c/${encodeURIComponent(command.conferenceId)}/recording#token=${command.joinToken}`;
         this.browser = await system.launchBrowser({
            display: this.display,
            sink: this.sinkName,
            url,
            width: config.video.width,
            height: config.video.height,
            ignoreHttpsErrors: config.ignoreHttpsErrors,
            debugPort: 9300 + this.displayNumber,
            hostResolverRules: config.hostResolverRules,
            diagnosticsFile: join(config.workDir, 'diagnostics', `${command.recordingId}.png`),
         });
         await this.browser.waitUntilReady(READY_TIMEOUT_MS);
         // one more moment for the scene to settle, so that the video does not start with a loading screen
         await sleep(1500);

         this.ffmpeg = system.spawn(
            'ffmpeg',
            buildRecordArgs({
               display: this.display,
               sink: this.sinkName,
               outFile: join(this.dir, OUT_FILE),
               ...config.video,
               maxSeconds: command.maxDurationMinutes * 60,
            }),
         );

         const early = await Promise.race([this.ffmpeg.exited, sleep(FFMPEG_STARTUP_CHECK_MS).then(() => 'running')]);
         if (early !== 'running') throw new Error('ffmpeg stopped right after it started');

         // after this the recording ends in one of three ways: stop requested, time limit, or the capture dies
         this.ffmpeg.exited.then(() => {
            if (!this.stopping) {
               log.warn('Capture ended on its own', { recordingId: command.recordingId });
               void this.stop('capture ended');
            }
         });
         this.browser.closed.then(() => {
            if (!this.stopping) {
               log.warn('The browser closed', { recordingId: command.recordingId });
               void this.stop('browser closed');
            }
         });
         this.maxDurationTimer = setTimeout(
            () => {
               log.info('Maximum duration reached', { recordingId: command.recordingId });
               void this.stop('maximum duration');
            },
            command.maxDurationMinutes * 60 * 1000,
         );

         log.info('Recording started', { recordingId: command.recordingId, conferenceId: command.conferenceId });
         await this.deps.reporter.report(command.recordingId, { event: 'started' });
      } catch (error) {
         await this.releaseResources();
         await rm(this.dir, { recursive: true, force: true });
         throw error;
      }
   }

   /** Ends the recording, stores it and reports. Safe to call several times. */
   stop(reason: string): Promise<void> {
      this.stopping ??= this.doStop(reason);
      return this.stopping;
   }

   private async doStop(reason: string): Promise<void> {
      const { log } = this.deps;
      const { command } = this;
      log.info('Stopping recording', { recordingId: command.recordingId, reason });

      if (this.maxDurationTimer) clearTimeout(this.maxDurationTimer);

      if (this.ffmpeg) {
         // "q" lets ffmpeg write the end of the file properly
         this.ffmpeg.quit();
         const exited = await Promise.race([
            this.ffmpeg.exited.then(() => true),
            sleep(FFMPEG_STOP_TIMEOUT_MS).then(() => false),
         ]);
         if (!exited) {
            log.warn('ffmpeg did not stop in time, killing it', { recordingId: command.recordingId });
            this.ffmpeg.kill();
            await this.ffmpeg.exited;
         }
      }

      await this.releaseResources();

      const meta: RecordingMeta = {
         recordingId: command.recordingId,
         conferenceId: command.conferenceId,
         storageKey: command.storageKey,
         startedAt: '',
      };

      try {
         await finalizeRecording(this.dir, meta, this.deps);
      } catch (error) {
         log.error('Finalizing the recording failed', { recordingId: command.recordingId, error: String(error) });
         await this.deps.reporter.report(command.recordingId, {
            event: 'failed',
            reason: 'The recording could not be processed.',
         });
      }
   }

   /** Close the browser, the virtual screen and the virtual sound card. */
   private async releaseResources(): Promise<void> {
      if (this.maxDurationTimer) clearTimeout(this.maxDurationTimer);

      await this.browser?.close();
      this.ffmpeg?.kill();
      this.xvfb?.kill();
      await this.removeSink?.();

      this.browser = undefined;
      this.xvfb = undefined;
      this.removeSink = undefined;
   }
}
