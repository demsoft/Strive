import { appendFile, mkdir, readFile, stat, writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import { buildStemMixArgs, StemInput } from './ffmpeg';
import { Logger } from './logger';
import { SystemOps } from './system';

const STEMS_DIR = 'stems';
const SEGMENTS_FILE = 'segments.json';
const ARCHIVE_FILE = 'tracks.zip';
/** shorter than a tenth of a second of sound */
const MIN_PIECE_BYTES = 600;

/** What the page tells about a piece of sound it records: one voice or one instrument for as long as it lasts. */
export type StemStart = {
   participantId: string;
   source: string;
   name: string;
   /** milliseconds since 1970 on the clock of this machine, when the recording of the piece started */
   startedAtMs: number;
};

export type StemSegment = StemStart & { id: number; file: string; endedAtMs?: number };

/**
 * Collects the separate tracks of a rehearsal. The recording page of the app records every voice and instrument it
 * plays as a piece of audio and hands the data over through three functions; this class writes them to the disk.
 */
export class StemCollector {
   private readonly segments: StemSegment[] = [];
   private nextId = 1;
   private writing: Promise<unknown> = Promise.resolve();

   constructor(
      private readonly dir: string,
      private readonly log: Logger,
   ) {}

   private get stemsDir() {
      return join(this.dir, STEMS_DIR);
   }

   /** Starts a piece, returns its id. */
   async start(info: StemStart): Promise<number> {
      await mkdir(this.stemsDir, { recursive: true });
      const id = this.nextId++;
      this.segments.push({ ...info, id, file: `${id}.webm` });
      await this.persist();
      return id;
   }

   /** Data of a piece arrives in small chunks while it is recorded. */
   async chunk(id: number, base64: string): Promise<void> {
      const segment = this.segments.find((x) => x.id === id);
      if (!segment) return;

      // the chunks of a piece must be written in the order they arrive
      this.writing = this.writing.then(() =>
         appendFile(join(this.stemsDir, segment.file), Buffer.from(base64, 'base64')).catch((error) =>
            this.log.warn('A piece of a track could not be written', { id, error: String(error) }),
         ),
      );
      await this.writing;
   }

   async end(id: number, endedAtMs: number): Promise<void> {
      const segment = this.segments.find((x) => x.id === id);
      if (!segment) return;

      segment.endedAtMs = endedAtMs;
      await this.writing;
      await this.persist();
   }

   private persist(): Promise<void> {
      return writeFile(join(this.stemsDir, SEGMENTS_FILE), JSON.stringify(this.segments)).catch((error) =>
         this.log.warn('The list of the tracks could not be saved', { error: String(error) }),
      );
   }
}

const SOURCE_LABELS: Record<string, string> = { mic: 'voice', instrument: 'instrument', 'screen-audio': 'shared-sound' };

/** A name that is safe as a file name on every system. */
export function safeFileName(text: string): string {
   const cleaned = text
      .normalize('NFC')
      .replace(/[^\p{L}\p{N} _.-]/gu, '')
      .replace(/\.{2,}/g, '.')
      .replace(/^\.+/, '')
      .replace(/\s+/g, ' ')
      .trim()
      .slice(0, 40);
   return cleaned || 'participant';
}

export type TrackPlan = { fileName: string; inputs: StemInput[] };

/**
 * Groups the pieces into tracks (one per person and kind of sound, a person may speak in several pieces when the
 * microphone is switched off and on) and puts every piece at its place on the timeline of the recording.
 *
 * @param startMs the moment the recording of the mix started (time zero of every track)
 */
export function planTracks(segments: StemSegment[], startMs: number): TrackPlan[] {
   const groups = new Map<string, StemSegment[]>();
   for (const segment of segments) {
      const key = `${segment.participantId}|${segment.source}`;
      groups.set(key, [...(groups.get(key) ?? []), segment]);
   }

   const used = new Set<string>();
   const plans: TrackPlan[] = [];
   for (const group of groups.values()) {
      const first = group[0];
      const label = SOURCE_LABELS[first.source] ?? safeFileName(first.source);
      const baseName = `${safeFileName(first.name)} - ${label}`;
      let fileName = `${baseName}.flac`;
      for (let n = 2; used.has(fileName.toLowerCase()); n++) fileName = `${baseName} (${n}).flac`;
      used.add(fileName.toLowerCase());

      plans.push({
         fileName,
         inputs: group
            .sort((a, b) => a.startedAtMs - b.startedAtMs)
            .map((x) => ({ file: x.file, offsetMs: Math.round(x.startedAtMs - startMs) })),
      });
   }

   return plans;
}

type ArchiveDeps = { system: SystemOps; log: Logger };

/**
 * Turns the collected pieces into one lossless file per track, all as long as the mix and starting together, and packs
 * them into an archive. Returns null if nothing was recorded.
 */
export async function buildTracksArchive(
   dir: string,
   startMs: number,
   totalSeconds: number,
   { system, log }: ArchiveDeps,
): Promise<string | null> {
   const stemsDir = join(dir, STEMS_DIR);
   let segments: StemSegment[];
   try {
      segments = JSON.parse(await readFile(join(stemsDir, SEGMENTS_FILE), 'utf8')) as StemSegment[];
   } catch {
      return null;
   }

   // a piece that only has the header of the container (a few bytes) holds no sound and would stop the whole track
   const sizes = await Promise.all(
      segments.map((x) =>
         stat(join(stemsDir, x.file)).then(
            (s) => s.size,
            () => 0,
         ),
      ),
   );
   segments = segments.filter((_, i) => sizes[i] >= MIN_PIECE_BYTES);
   if (segments.length === 0) return null;

   const plans = planTracks(segments, startMs);
   const written: string[] = [];
   for (const plan of plans) {
      const out = join(stemsDir, plan.fileName);
      try {
         await system.run(
            'ffmpeg',
            buildStemMixArgs(
               plan.inputs.map((x) => ({ ...x, file: join(stemsDir, x.file) })),
               totalSeconds,
               out,
            ),
         );
         written.push(out);
      } catch (error) {
         // the other tracks are still worth keeping
         log.warn('A track could not be processed', { fileName: plan.fileName, error: String(error) });
      }
   }

   if (written.length === 0) return null;

   const archive = join(dir, ARCHIVE_FILE);
   // the audio is compressed already: store only (-0), without the folders (-j)
   await system.run('zip', ['-0', '-j', '-q', archive, ...written]);
   return archive;
}

/**
 * The script that runs inside of the recording page. The page marks every sound it plays with data attributes
 * (audio elements with data-stem-id, data-stem-source, data-stem-name); this records each of them as pieces of
 * audio and hands the data over to the functions that the recorder exposes (see system.ts).
 */
export const STEM_CAPTURE_SCRIPT = `(() => {
  const active = new Map();
  let flushing = false;
  const toBase64 = (buffer) => {
    let binary = '';
    const bytes = new Uint8Array(buffer);
    for (let i = 0; i < bytes.length; i += 0x8000) binary += String.fromCharCode.apply(null, bytes.subarray(i, i + 0x8000));
    return btoa(binary);
  };
  const stop = (key) => {
    const entry = active.get(key);
    if (!entry) return;
    active.delete(key);
    try { if (entry.recorder.state !== 'inactive') entry.recorder.stop(); } catch (e) {}
  };
  const start = (key, el, track) => {
    const recorder = new MediaRecorder(new MediaStream([track]), { mimeType: 'audio/webm;codecs=opus', audioBitsPerSecond: 256000 });
    const entry = { recorder, track, id: null, queue: Promise.resolve() };
    active.set(key, entry);
    recorder.onstart = () => {
      entry.queue = entry.queue.then(async () => {
        entry.id = await window.__striveStemStart({
          participantId: el.dataset.stemId, source: el.dataset.stemSource, name: el.dataset.stemName || '', startedAtMs: Date.now(),
        });
      });
    };
    recorder.ondataavailable = (e) => {
      if (!e.data || e.data.size === 0) return;
      entry.queue = entry.queue.then(async () => {
        if (entry.id !== null) await window.__striveStemChunk(entry.id, toBase64(await e.data.arrayBuffer()));
      });
    };
    recorder.onstop = () => {
      entry.queue = entry.queue.then(async () => {
        if (entry.id !== null) await window.__striveStemEnd(entry.id, Date.now());
      });
    };
    recorder.start(1000);
  };
  const scan = () => {
    if (flushing) return;
    const seen = new Set();
    document.querySelectorAll('audio[data-stem-id]').forEach((el) => {
      const key = el.dataset.stemId + '|' + el.dataset.stemSource;
      const track = el.srcObject && el.srcObject.getAudioTracks ? el.srcObject.getAudioTracks()[0] : null;
      if (!track || track.readyState !== 'live') return;
      seen.add(key);
      const current = active.get(key);
      if (current && current.track !== track) stop(key);
      if (!active.has(key)) start(key, el, track);
    });
    for (const key of [...active.keys()]) if (!seen.has(key)) stop(key);
  };
  setInterval(scan, 500);
  window.__striveStemFlush = async () => {
    // no new pieces after this, the scan must not restart what is stopped here
    flushing = true;
    const entries = [...active.values()];
    for (const key of [...active.keys()]) stop(key);
    // the recorders deliver their last data and their end a moment after stop()
    await new Promise((resolve) => setTimeout(resolve, 300));
    await Promise.all(entries.map((x) => x.queue));
  };
})();`;
