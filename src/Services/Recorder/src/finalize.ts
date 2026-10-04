import { mkdir, readFile, rm, stat, writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import { buildProbeArgs, buildRemuxArgs, parseDuration } from './ffmpeg';
import { Logger } from './logger';
import { SystemOps } from './system';
import { Reporter, Uploader } from './types';

export type RecordingMeta = {
   recordingId: string;
   conferenceId: string;
   storageKey: string;
   startedAt: string;
};

export const OUT_FILE = 'out.mp4';
const FINAL_FILE = 'final.mp4';
const META_FILE = 'meta.json';
const DONE_FILE = 'done';

/** Files smaller than this contain no usable video (only the container header). */
const MIN_RECORDING_BYTES = 4 * 1024;
const UPLOAD_ATTEMPTS = 3;

export async function writeMeta(dir: string, meta: RecordingMeta): Promise<void> {
   await mkdir(dir, { recursive: true });
   await writeFile(join(dir, META_FILE), JSON.stringify(meta));
}

export async function readMeta(dir: string): Promise<RecordingMeta | null> {
   try {
      return JSON.parse(await readFile(join(dir, META_FILE), 'utf8')) as RecordingMeta;
   } catch {
      return null;
   }
}

export async function isDone(dir: string): Promise<boolean> {
   return stat(join(dir, DONE_FILE)).then(
      () => true,
      () => false,
   );
}

type Deps = { system: SystemOps; uploader: Uploader; reporter: Reporter; log: Logger; retryDelayMs?: number };

/**
 * Turn the recorded file into a stored recording: make it seekable, upload it and tell the server. Used right after
 * a recording ended and by the recovery after the recorder was restarted, so the same code handles both.
 */
export async function finalizeRecording(dir: string, meta: RecordingMeta, deps: Deps): Promise<void> {
   const { system, uploader, reporter, log } = deps;
   const file = join(dir, OUT_FILE);
   const finalFile = join(dir, FINAL_FILE);

   const size = await stat(file).then(
      (s) => s.size,
      () => 0,
   );

   if (size < MIN_RECORDING_BYTES) {
      log.error('The recording is empty', { recordingId: meta.recordingId, size });
      await reporter.report(meta.recordingId, { event: 'failed', reason: 'The recording is empty.' });
      await rm(dir, { recursive: true, force: true });
      return;
   }

   // the fragmented file is playable but browsers cannot seek in it, copy the streams into a regular MP4
   let toUpload = file;
   try {
      await system.run('ffmpeg', buildRemuxArgs(file, finalFile));
      toUpload = finalFile;
   } catch (error) {
      log.warn('Making the recording seekable failed, uploading it as it is', {
         recordingId: meta.recordingId,
         error: String(error),
      });
   }

   const durationSeconds = await system
      .run('ffprobe', buildProbeArgs(toUpload))
      .then(parseDuration)
      .catch(() => null);

   let uploadedBytes: number | null = null;
   for (let attempt = 1; attempt <= UPLOAD_ATTEMPTS && uploadedBytes === null; attempt++) {
      try {
         uploadedBytes = await uploader.upload(toUpload, meta.storageKey);
      } catch (error) {
         log.error('Upload failed', { recordingId: meta.recordingId, attempt, error: String(error) });
         if (attempt < UPLOAD_ATTEMPTS) await new Promise((r) => setTimeout(r, deps.retryDelayMs ?? 5000 * attempt));
      }
   }

   if (uploadedBytes === null) {
      // keep the file, there is something to rescue by hand
      await reporter.report(meta.recordingId, { event: 'failed', reason: 'The recording could not be uploaded.' });
      await writeFile(join(dir, DONE_FILE), 'upload failed');
      log.error('The recording stays on the disk of the recorder', { recordingId: meta.recordingId, dir });
      return;
   }

   await reporter.report(meta.recordingId, {
      event: 'finished',
      storageKey: meta.storageKey,
      sizeBytes: uploadedBytes,
      durationSeconds: durationSeconds ?? undefined,
   });

   await rm(dir, { recursive: true, force: true });
   log.info('Recording stored', { recordingId: meta.recordingId, sizeBytes: uploadedBytes, durationSeconds });
}
