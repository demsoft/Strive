import { readdir } from 'node:fs/promises';
import { join } from 'node:path';
import { Config } from './config';
import { finalizeRecording, isDone, readMeta } from './finalize';
import { Logger } from './logger';
import { RecordingSession, SessionDeps } from './session';
import { DuplicateRecordingError, RecorderBusyError, StartCommand } from './types';

const ID_PATTERN = /^[A-Za-z0-9_-]{1,100}$/;
/** the first X display number, below are real ones */
const FIRST_DISPLAY = 100;

/** Reject anything that could escape the work directory or the storage prefix. */
export function validateCommand(command: StartCommand): string | null {
   if (!ID_PATTERN.test(command.recordingId ?? '')) return 'recordingId is invalid';
   if (!ID_PATTERN.test(command.conferenceId ?? '')) return 'conferenceId is invalid';
   if (typeof command.joinToken !== 'string' || command.joinToken.length < 20) return 'joinToken is missing';
   if (!/^recordings\/[A-Za-z0-9_-]+\/[A-Za-z0-9_-]+\.mp4$/.test(command.storageKey ?? '')) return 'storageKey is invalid';
   if (!Number.isInteger(command.maxDurationMinutes) || command.maxDurationMinutes < 1 || command.maxDurationMinutes > 24 * 60)
      return 'maxDurationMinutes is invalid';
   return null;
}

export type SessionFactory = (command: StartCommand, displayNumber: number, deps: SessionDeps) => Pick<RecordingSession, 'start' | 'stop' | 'command'>;

/** Keeps track of the running recordings, enforces the capacity and starts, stops and recovers them. */
export class RecorderManager {
   private readonly sessions = new Map<string, { session: Pick<RecordingSession, 'start' | 'stop' | 'command'>; display: number }>();

   constructor(
      private readonly deps: SessionDeps,
      private readonly log: Logger,
      private readonly createSession: SessionFactory = (command, display, deps) => new RecordingSession(command, display, deps),
   ) {}

   get activeCount() {
      return this.sessions.size;
   }

   has(recordingId: string) {
      return this.sessions.has(recordingId);
   }

   /**
    * Accepts the recording and starts it in the background: joining a conference and starting a browser takes
    * seconds. A failure is reported to the server asynchronously.
    */
   start(command: StartCommand): void {
      const problem = validateCommand(command);
      if (problem) throw new RangeError(problem);

      if (this.sessions.has(command.recordingId)) throw new DuplicateRecordingError(command.recordingId);
      if (this.sessions.size >= this.deps.config.maxConcurrentRecordings) throw new RecorderBusyError();

      const display = this.freeDisplay();
      const session = this.createSession(command, display, this.deps);
      this.sessions.set(command.recordingId, { session, display });

      void session
         .start()
         .catch(async (error) => {
            this.log.error('Recording could not be started', { recordingId: command.recordingId, error: String(error) });
            await this.deps.reporter.report(command.recordingId, {
               event: 'failed',
               reason: 'The recorder could not join the conference.',
            });
            this.sessions.delete(command.recordingId);
         });
   }

   /** @returns false if there is no such recording (it may be over already, stopping is idempotent) */
   async stop(recordingId: string, reason = 'requested'): Promise<boolean> {
      const entry = this.sessions.get(recordingId);
      if (!entry) return false;

      try {
         await entry.session.stop(reason);
      } finally {
         this.sessions.delete(recordingId);
      }
      return true;
   }

   /** Finish every running recording, used when the container is stopped. */
   async shutdown(): Promise<void> {
      await Promise.all([...this.sessions.keys()].map((id) => this.stop(id, 'shutdown')));
   }

   /**
    * After a restart or crash: recordings that were running leave files behind. Upload and report them, a
    * fragmented MP4 is playable even if it was cut off.
    */
   async recover(): Promise<number> {
      const { workDir } = this.deps.config;
      let entries: string[];
      try {
         entries = await readdir(workDir);
      } catch {
         return 0;
      }

      let recovered = 0;
      for (const entry of entries) {
         const dir = join(workDir, entry);
         if (this.sessions.has(entry) || (await isDone(dir))) continue;

         const meta = await readMeta(dir);
         if (!meta) continue;

         this.log.warn('Recovering an unfinished recording', { recordingId: meta.recordingId });
         try {
            await finalizeRecording(dir, meta, this.deps);
            recovered++;
         } catch (error) {
            this.log.error('Recovery failed', { recordingId: meta.recordingId, error: String(error) });
         }
      }

      return recovered;
   }

   private freeDisplay(): number {
      const used = new Set([...this.sessions.values()].map((x) => x.display));
      let display = FIRST_DISPLAY;
      while (used.has(display)) display++;
      return display;
   }
}

export type { Config };
