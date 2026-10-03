/** What the Strive server sends to start a recording (RecorderStartCommand on the server). */
export type StartCommand = {
   recordingId: string;
   conferenceId: string;
   joinToken: string;
   storageKey: string;
   maxDurationMinutes: number;
};

/** What the recorder reports back (RecorderReport on the server, enums in camel case). */
export type Report = {
   event: 'started' | 'finished' | 'failed';
   storageKey?: string;
   sizeBytes?: number;
   durationSeconds?: number;
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
