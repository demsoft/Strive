/** What the Strive server sends to start a recording (RecorderStartCommand on the server). */
export type StartCommand = {
   recordingId: string;
   conferenceId: string;
   joinToken: string;
   storageKey: string;
   maxDurationMinutes: number;
   /** a meeting is recorded as a video; a rehearsal as sound only, with a track for everybody (default: meeting) */
   kind?: 'meeting' | 'rehearsal';
   /** where the archive with the tracks is stored (rehearsals) */
   tracksStorageKey?: string | null;
};

/** What the recorder reports back (RecorderReport on the server, enums in camel case). */
export type Report = {
   event: 'started' | 'finished' | 'failed';
   storageKey?: string;
   sizeBytes?: number;
   durationSeconds?: number;
   tracksStorageKey?: string;
   tracksSizeBytes?: number;
   reason?: string;
};

export interface Reporter {
   report(recordingId: string, report: Report): Promise<void>;
}

export interface Uploader {
   /** Upload the file in parts, returns the size in bytes. */
   upload(file: string, storageKey: string): Promise<number>;
}

export class RecorderBusyError extends Error {}
export class DuplicateRecordingError extends Error {}
