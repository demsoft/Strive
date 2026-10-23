import { MAX_VIDEO_BYTES, MAX_VIDEO_SECONDS } from './limits';

/** How long the video is, as far as the browser can tell (null: it cannot, then the server decides). */
export function readDuration(file: Blob): Promise<number | null> {
   return new Promise((resolve) => {
      const url = URL.createObjectURL(file);
      const element = document.createElement('video');
      const done = (value: number | null) => {
         URL.revokeObjectURL(url);
         element.removeAttribute('src');
         resolve(value);
      };

      element.preload = 'metadata';
      element.onloadedmetadata = () => done(Number.isFinite(element.duration) ? element.duration : null);
      element.onerror = () => done(null);
      element.src = url;
   });
}

export type FileProblem = 'not_video' | 'too_big' | 'too_long';

/** What is wrong with a chosen file, seen from the page (the server checks again). Null: nothing. */
export function fileProblem(file: { type: string; size: number }, durationSeconds: number | null): FileProblem | null {
   if (!file.type.startsWith('video/')) return 'not_video';
   if (file.size <= 0 || file.size > MAX_VIDEO_BYTES) return 'too_big';
   if (durationSeconds !== null && durationSeconds > MAX_VIDEO_SECONDS) return 'too_long';
   return null;
}

/** 95 -> "1:35", 3 -> "0:03" */
export function formatDuration(seconds: number | null | undefined): string {
   if (seconds === null || seconds === undefined || !Number.isFinite(seconds) || seconds < 0) return '';
   const whole = Math.round(seconds);
   return `${Math.floor(whole / 60)}:${String(whole % 60).padStart(2, '0')}`;
}

export const formatMegabytes = (bytes: number) => `${Math.max(1, Math.round(bytes / (1024 * 1024)))} MB`;
